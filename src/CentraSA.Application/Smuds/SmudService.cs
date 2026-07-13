using System.Text.Json;
using CentraSA.Application.Common;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;

namespace CentraSA.Application.Smuds;

public sealed class SmudService(
    ISmudRepository repository,
    TimeProvider timeProvider) : ISmudService
{
    public async Task<SmudBoardData> SearchAsync(
        SmudSearch search,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Smud> smuds = await repository.SearchAsync(search, cancellationToken);
        SmudReferenceData referenceData = await repository.GetReferenceDataAsync(cancellationToken);
        return MapBoard(smuds, referenceData, search);
    }

    public Task<SmudBoardData> GetPresentationAsync(
        bool showFinalized,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = GetToday();
        return SearchAsync(
            new SmudSearch(
                Search: null,
                AreaId: null,
                PersonId: null,
                StatusId: null,
                DueFilter: SmudDueFilter.All,
                ActionRequiredOnly: false,
                HideFinalized: !showFinalized,
                ArchivedOnly: false,
                Today: today),
            cancellationToken);
    }

    public async Task<SmudEditorData> GetCreateEditorAsync(CancellationToken cancellationToken = default)
    {
        SmudReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        StatusDefinition defaultStatus = GetDefaultActiveStatus(data);
        var input = new SmudInput
        {
            ResponsibleAreaId = data.Areas.Count > 0 ? data.Areas[0].Id : Guid.Empty,
            StatusId = defaultStatus.Id,
            Priority = PriorityLevel.Medium,
            OpenedOn = GetToday(),
            Version = 1,
        };

        return new SmudEditorData(null, input, MapFormOptions(data));
    }

    public async Task<SmudEditorData?> GetEditEditorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Smud? smud = await repository.GetByIdAsync(id, includeArchived: false, track: false, cancellationToken);
        if (smud is null)
        {
            return null;
        }

        SmudReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        var input = new SmudInput
        {
            Code = smud.Code,
            Title = smud.Title,
            Description = smud.Description,
            ResponsibleAreaId = smud.ResponsibleAreaId,
            ResponsiblePersonId = smud.ResponsiblePersonId,
            StatusId = smud.StatusDefinitionId,
            Priority = smud.Priority,
            OpenedOn = smud.OpenedOn,
            DueDate = smud.DueDate,
            RequiredAction = smud.RequiredAction,
            Notes = smud.Notes,
            Version = smud.Version,
        };

        return new SmudEditorData(smud.Id, input, MapFormOptions(data));
    }

    public async Task<SmudDetailsData?> GetDetailsAsync(
        Guid id,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        Smud? smud = await repository.GetByIdAsync(id, includeArchived, track: false, cancellationToken);
        if (smud is null)
        {
            return null;
        }

        IReadOnlyList<ActivityHistory> history = await repository.GetHistoryAsync(id, cancellationToken);
        return new SmudDetailsData(
            MapCard(smud, GetToday()),
            smud.Notes,
            smud.CreatedAtUtc,
            smud.UpdatedAtUtc,
            smud.CompletedAtUtc,
            smud.ArchivedAtUtc,
            history.Select(item => new SmudHistoryItem(
                TranslateAction(item.ActionType),
                item.Summary,
                item.OccurredAtUtc)).ToList());
    }

    public async Task<SmudOperationResult> CreateAsync(
        SmudInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        SmudReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        if (!TryNormalizeCode(input.Code, out string normalizedCode, out string? codeError))
        {
            return SmudOperationResult.Invalid(codeError!);
        }

        string[] errors = Validate(input, data);
        if (errors.Length > 0)
        {
            return SmudOperationResult.Invalid(errors);
        }

        if (await repository.IsCodeInUseAsync(normalizedCode, excludingId: null, cancellationToken))
        {
            return SmudOperationResult.DuplicateCode();
        }

        StatusDefinition status = data.Statuses.Single(item => item.Id == input.StatusId);
        DateTime now = GetUtcNow();
        var smud = new Smud
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        ApplyInput(smud, input, normalizedCode);
        ApplyLifecycle(smud, status.LifecycleState, now);
        repository.Add(smud);
        AddHistory(smud.Id, actorUserId, ActivityActionType.Created, $"{normalizedCode} criado.");

        return await SaveOperationAsync(smud.Id, cancellationToken);
    }

    public async Task<SmudOperationResult> UpdateAsync(
        Guid id,
        SmudInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Smud? smud = await repository.GetByIdAsync(id, includeArchived: false, track: true, cancellationToken);
        if (smud is null)
        {
            return SmudOperationResult.NotFound();
        }

        if (smud.Version != input.Version)
        {
            return SmudOperationResult.Conflict(id);
        }

        SmudReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        if (!TryNormalizeCode(input.Code, out string normalizedCode, out string? codeError))
        {
            return SmudOperationResult.Invalid(codeError!);
        }

        string[] errors = Validate(input, data);
        if (errors.Length > 0)
        {
            return SmudOperationResult.Invalid(errors);
        }

        if (await repository.IsCodeInUseAsync(normalizedCode, id, cancellationToken))
        {
            return SmudOperationResult.DuplicateCode(id);
        }

        StatusDefinition newStatus = data.Statuses.Single(status => status.Id == input.StatusId);
        string oldCode = smud.Code;
        string oldTitle = smud.Title;
        string? oldDescription = smud.Description;
        string? oldRequiredAction = smud.RequiredAction;
        string? oldNotes = smud.Notes;
        PriorityLevel oldPriority = smud.Priority;
        DateOnly? oldOpenedOn = smud.OpenedOn;
        DateOnly? oldDueDate = smud.DueDate;
        Guid oldAreaId = smud.ResponsibleAreaId;
        Guid? oldPersonId = smud.ResponsiblePersonId;
        Guid oldStatusId = smud.StatusDefinitionId;
        string oldStatusName = smud.StatusDefinition.Name;
        LifecycleState oldLifecycle = smud.StatusDefinition.LifecycleState;
        DateTime now = GetUtcNow();

        ApplyInput(smud, input, normalizedCode);
        smud.UpdatedAtUtc = now;
        ApplyLifecycle(smud, newStatus.LifecycleState, now);

        if (oldStatusId != smud.StatusDefinitionId)
        {
            AddHistory(
                smud.Id,
                actorUserId,
                ActivityActionType.StatusChanged,
                $"Status alterado de '{oldStatusName}' para '{newStatus.Name}'.",
                new { Before = oldStatusId, After = smud.StatusDefinitionId });

            if (newStatus.LifecycleState == LifecycleState.Completed && oldLifecycle != LifecycleState.Completed)
            {
                AddHistory(smud.Id, actorUserId, ActivityActionType.Completed, $"{normalizedCode} concluído.");
            }
            else if (newStatus.LifecycleState == LifecycleState.Active && oldLifecycle != LifecycleState.Active)
            {
                AddHistory(smud.Id, actorUserId, ActivityActionType.Reopened, $"{normalizedCode} reaberto.");
            }
        }

        if (oldAreaId != smud.ResponsibleAreaId || oldPersonId != smud.ResponsiblePersonId)
        {
            AddHistory(
                smud.Id,
                actorUserId,
                ActivityActionType.ResponsibleChanged,
                "Área ou pessoa responsável alterada.",
                new
                {
                    AreaBefore = oldAreaId,
                    AreaAfter = smud.ResponsibleAreaId,
                    PersonBefore = oldPersonId,
                    PersonAfter = smud.ResponsiblePersonId,
                });
        }

        if (oldDueDate != smud.DueDate)
        {
            AddHistory(
                smud.Id,
                actorUserId,
                ActivityActionType.DueDateChanged,
                "Prazo do SMUD alterado.",
                new { Before = oldDueDate, After = smud.DueDate });
        }

        if (oldCode != smud.Code
            || oldTitle != smud.Title
            || oldDescription != smud.Description
            || oldRequiredAction != smud.RequiredAction
            || oldNotes != smud.Notes
            || oldPriority != smud.Priority
            || oldOpenedOn != smud.OpenedOn)
        {
            AddHistory(smud.Id, actorUserId, ActivityActionType.Updated, "Dados do SMUD atualizados.");
        }

        return await SaveOperationAsync(smud.Id, cancellationToken);
    }

    public async Task<SmudOperationResult> ArchiveAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Smud? smud = await repository.GetByIdAsync(id, includeArchived: false, track: true, cancellationToken);
        if (smud is null)
        {
            return SmudOperationResult.NotFound();
        }

        if (smud.Version != version)
        {
            return SmudOperationResult.Conflict(id);
        }

        DateTime now = GetUtcNow();
        smud.ArchivedAtUtc = now;
        smud.UpdatedAtUtc = now;
        AddHistory(id, actorUserId, ActivityActionType.Archived, $"{smud.Code} arquivado.");
        return await SaveOperationAsync(id, cancellationToken);
    }

    public async Task<SmudOperationResult> RestoreAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Smud? smud = await repository.GetByIdAsync(id, includeArchived: true, track: true, cancellationToken);
        if (smud is null || smud.ArchivedAtUtc is null)
        {
            return SmudOperationResult.NotFound();
        }

        if (smud.Version != version)
        {
            return SmudOperationResult.Conflict(id);
        }

        DateTime now = GetUtcNow();
        smud.ArchivedAtUtc = null;
        smud.UpdatedAtUtc = now;
        AddHistory(id, actorUserId, ActivityActionType.Restored, $"{smud.Code} restaurado para o quadro.");
        return await SaveOperationAsync(id, cancellationToken);
    }

    private async Task<SmudOperationResult> SaveOperationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return SmudOperationResult.Success(id);
        }
        catch (DuplicateSmudCodeException)
        {
            return SmudOperationResult.DuplicateCode(id);
        }
        catch (ConcurrencyConflictException)
        {
            return SmudOperationResult.Conflict(id);
        }
    }

    private void AddHistory(
        Guid smudId,
        Guid actorUserId,
        ActivityActionType action,
        string summary,
        object? changes = null)
    {
        repository.AddHistory(new ActivityHistory
        {
            Id = Guid.NewGuid(),
            EntityType = TrackedEntityType.Smud,
            EntityId = smudId,
            ActionType = action,
            OccurredAtUtc = GetUtcNow(),
            ActorUserId = actorUserId,
            Summary = summary,
            ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes),
        });
    }

    private static string[] Validate(SmudInput input, SmudReferenceData data)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            errors.Add("Informe o título do SMUD.");
        }
        else if (input.Title.Trim().Length > 200)
        {
            errors.Add("O título deve ter no máximo 200 caracteres.");
        }

        AddLengthError(input.Description, 4000, "A descrição deve ter no máximo 4.000 caracteres.", errors);
        AddLengthError(input.RequiredAction, 1000, "A ação necessária deve ter no máximo 1.000 caracteres.", errors);
        AddLengthError(input.Notes, 4000, "As observações devem ter no máximo 4.000 caracteres.", errors);

        if (!data.Areas.Any(area => area.Id == input.ResponsibleAreaId))
        {
            errors.Add("Selecione uma área responsável válida.");
        }

        if (!data.Statuses.Any(status => status.Id == input.StatusId))
        {
            errors.Add("Selecione um status válido para SMUDs.");
        }

        if (input.ResponsiblePersonId.HasValue && !data.People.Any(person => person.Id == input.ResponsiblePersonId))
        {
            errors.Add("Selecione uma pessoa responsável válida.");
        }

        if (!Enum.IsDefined(input.Priority))
        {
            errors.Add("Selecione uma prioridade válida.");
        }

        return errors.ToArray();
    }

    private static void AddLengthError(
        string? value,
        int maximumLength,
        string message,
        List<string> errors)
    {
        if (value?.Trim().Length > maximumLength)
        {
            errors.Add(message);
        }
    }

    private static bool TryNormalizeCode(
        string code,
        out string normalizedCode,
        out string? error)
    {
        try
        {
            normalizedCode = SmudCodeNormalizer.Normalize(code);
            error = null;
            return true;
        }
        catch (ArgumentException)
        {
            normalizedCode = string.Empty;
            error = "Informe o código do SMUD.";
            return false;
        }
        catch (FormatException exception)
        {
            normalizedCode = string.Empty;
            error = exception.Message;
            return false;
        }
    }

    private static void ApplyInput(Smud smud, SmudInput input, string normalizedCode)
    {
        smud.Code = normalizedCode;
        smud.NormalizedCode = normalizedCode;
        smud.Title = input.Title.Trim();
        smud.Description = NormalizeOptional(input.Description);
        smud.ResponsibleAreaId = input.ResponsibleAreaId;
        smud.ResponsiblePersonId = input.ResponsiblePersonId;
        smud.StatusDefinitionId = input.StatusId;
        smud.Priority = input.Priority;
        smud.OpenedOn = input.OpenedOn;
        smud.DueDate = input.DueDate;
        smud.RequiredAction = NormalizeOptional(input.RequiredAction);
        smud.Notes = NormalizeOptional(input.Notes);
    }

    private static void ApplyLifecycle(Smud smud, LifecycleState lifecycleState, DateTime now)
    {
        smud.CompletedAtUtc = lifecycleState == LifecycleState.Completed
            ? smud.CompletedAtUtc ?? now
            : null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StatusDefinition GetDefaultActiveStatus(SmudReferenceData data) =>
        data.Statuses.FirstOrDefault(status => status.Code == "PENDING_CENTRASA" && status.LifecycleState == LifecycleState.Active)
        ?? data.Statuses.First(status => status.LifecycleState == LifecycleState.Active);

    private static SmudFormOptions MapFormOptions(SmudReferenceData data) => new(
        data.Areas.Select(area => new SmudLookupOption(area.Id, area.Name, area.ColorToken)).ToList(),
        data.People.Select(person => new SmudLookupOption(
            person.Id,
            person.DisplayName,
            Group: person.TeamArea?.Name)).ToList(),
        data.Statuses.Select(status => new SmudLookupOption(
            status.Id,
            status.Name,
            status.ColorToken)).ToList());

    private static SmudFilterOptions MapFilterOptions(SmudReferenceData data) => new(
        data.Areas.Select(area => new SmudLookupOption(area.Id, area.Name, area.ColorToken)).ToList(),
        data.People.Select(person => new SmudLookupOption(person.Id, person.DisplayName)).ToList(),
        data.Statuses.Select(status => new SmudLookupOption(
            status.Id,
            status.Name,
            status.ColorToken)).ToList());

    private static SmudBoardData MapBoard(
        IReadOnlyList<Smud> smuds,
        SmudReferenceData data,
        SmudSearch search)
    {
        IEnumerable<StatusDefinition> statuses = data.Statuses;
        if (search.StatusId.HasValue)
        {
            statuses = statuses.Where(status => status.Id == search.StatusId.Value);
        }

        if (search.HideFinalized)
        {
            statuses = statuses.Where(status => status.LifecycleState == LifecycleState.Active);
        }

        List<SmudBoardColumn> columns = statuses
            .OrderBy(status => status.SortOrder)
            .Select(status => new SmudBoardColumn(
                status.Id,
                status.Name,
                status.ColorToken,
                status.LifecycleState,
                status.SortOrder,
                smuds.Where(smud => smud.StatusDefinitionId == status.Id)
                    .OrderByDescending(smud => WorkItemDeadlineRules.IsOverdue(
                        smud.DueDate,
                        smud.StatusDefinition.LifecycleState,
                        smud.ArchivedAtUtc,
                        search.Today))
                    .ThenBy(smud => smud.DueDate.HasValue ? 0 : 1)
                    .ThenBy(smud => smud.DueDate)
                    .ThenBy(smud => smud.NormalizedCode)
                    .Select(smud => MapCard(smud, search.Today))
                    .ToList()))
            .ToList();

        return new SmudBoardData(columns, MapFilterOptions(data), search.ArchivedOnly);
    }

    private static SmudBoardCard MapCard(Smud smud, DateOnly today) => new(
        smud.Id,
        smud.Code,
        smud.Title,
        smud.Description,
        smud.ResponsibleArea.Name,
        smud.ResponsiblePerson?.DisplayName,
        smud.StatusDefinition.Name,
        smud.StatusDefinition.ColorToken,
        smud.StatusDefinition.LifecycleState,
        smud.Priority,
        smud.OpenedOn,
        smud.DueDate,
        smud.RequiredAction,
        WorkItemDeadlineRules.IsOverdue(
            smud.DueDate,
            smud.StatusDefinition.LifecycleState,
            smud.ArchivedAtUtc,
            today),
        WorkItemDeadlineRules.IsDueSoon(
            smud.DueDate,
            smud.StatusDefinition.LifecycleState,
            smud.ArchivedAtUtc,
            today),
        SmudOperationalRules.RequiresAction(
            smud.RequiredAction,
            smud.StatusDefinition.LifecycleState,
            smud.ArchivedAtUtc),
        smud.Version);

    private static string TranslateAction(ActivityActionType action) => action switch
    {
        ActivityActionType.Created => "Criação",
        ActivityActionType.Updated => "Alteração",
        ActivityActionType.StatusChanged => "Mudança de status",
        ActivityActionType.ResponsibleChanged => "Responsável alterado",
        ActivityActionType.DueDateChanged => "Prazo alterado",
        ActivityActionType.Completed => "Conclusão",
        ActivityActionType.Reopened => "Reabertura",
        ActivityActionType.Archived => "Arquivamento",
        ActivityActionType.Restored => "Restauração",
        _ => "Alteração",
    };

    private DateOnly GetToday() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
