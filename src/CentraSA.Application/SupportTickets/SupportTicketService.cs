using System.Text.Json;
using CentraSA.Application.Common;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;

namespace CentraSA.Application.SupportTickets;

public sealed class SupportTicketService(
    ISupportTicketRepository repository,
    TimeProvider timeProvider) : ISupportTicketService
{
    public async Task<SupportTicketBoardData> SearchAsync(
        SupportTicketSearch search,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SupportTicket> tickets = await repository.SearchAsync(search, cancellationToken);
        SupportTicketReferenceData references = await repository.GetReferenceDataAsync(cancellationToken);
        return MapBoard(tickets, references, search);
    }

    public Task<SupportTicketBoardData> GetPresentationAsync(
        bool showFinalized,
        CancellationToken cancellationToken = default) =>
        SearchAsync(
            new SupportTicketSearch(
                Search: null,
                CategoryId: null,
                AreaId: null,
                PersonId: null,
                StatusId: null,
                DueFilter: SupportTicketDueFilter.All,
                ActionRequiredOnly: false,
                HideFinalized: !showFinalized,
                ArchivedOnly: false,
                Today: GetToday()),
            cancellationToken);

    public async Task<SupportTicketEditorData> GetCreateEditorAsync(
        CancellationToken cancellationToken = default)
    {
        SupportTicketReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        var input = new SupportTicketInput
        {
            CategoryId = data.Categories.Count > 0 ? data.Categories[0].Id : Guid.Empty,
            ResponsibleAreaId = data.Areas.Count > 0 ? data.Areas[0].Id : Guid.Empty,
            StatusId = GetDefaultActiveStatus(data).Id,
            Priority = PriorityLevel.Medium,
            OpenedOn = GetToday(),
            Version = 1,
        };

        return new SupportTicketEditorData(null, input, MapFormOptions(data));
    }

    public async Task<SupportTicketEditorData?> GetEditEditorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await repository.GetByIdAsync(
            id,
            includeArchived: false,
            track: false,
            cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        SupportTicketReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        var input = new SupportTicketInput
        {
            Number = ticket.TicketNumber,
            Title = ticket.Title,
            Description = ticket.Description,
            CategoryId = ticket.CategoryId,
            ResponsibleAreaId = ticket.ResponsibleAreaId,
            ResponsiblePersonId = ticket.ResponsiblePersonId,
            StatusId = ticket.StatusDefinitionId,
            Priority = ticket.Priority,
            OpenedOn = ticket.OpenedOn,
            DueDate = ticket.DueDate,
            PendingAction = ticket.PendingAction,
            Notes = ticket.Notes,
            Version = ticket.Version,
        };

        return new SupportTicketEditorData(ticket.Id, input, MapFormOptions(data));
    }

    public async Task<SupportTicketDetailsData?> GetDetailsAsync(
        Guid id,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await repository.GetByIdAsync(
            id,
            includeArchived,
            track: false,
            cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        IReadOnlyList<ActivityHistory> history = await repository.GetHistoryAsync(id, cancellationToken);
        return new SupportTicketDetailsData(
            MapCard(ticket, GetToday()),
            ticket.Notes,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.CompletedAtUtc,
            ticket.ArchivedAtUtc,
            history.Select(item => new SupportTicketHistoryItem(
                TranslateAction(item.ActionType),
                item.Summary,
                item.OccurredAtUtc)).ToList());
    }

    public async Task<SupportTicketOperationResult> CreateAsync(
        SupportTicketInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        SupportTicketReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        if (!TryNormalizeNumber(input.Number, out string normalizedNumber, out string? numberError))
        {
            return SupportTicketOperationResult.Invalid(numberError!);
        }

        string[] errors = Validate(input, data);
        if (errors.Length > 0)
        {
            return SupportTicketOperationResult.Invalid(errors);
        }

        if (await repository.IsNumberInUseAsync(normalizedNumber, excludingId: null, cancellationToken))
        {
            return SupportTicketOperationResult.DuplicateNumber();
        }

        StatusDefinition status = data.Statuses.Single(item => item.Id == input.StatusId);
        DateTime now = GetUtcNow();
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        ApplyInput(ticket, input, normalizedNumber);
        ApplyLifecycle(ticket, status.LifecycleState, now);
        repository.Add(ticket);
        AddHistory(ticket.Id, actorUserId, ActivityActionType.Created, $"Chamado {normalizedNumber} criado.");
        return await SaveOperationAsync(ticket.Id, cancellationToken);
    }

    public async Task<SupportTicketOperationResult> UpdateAsync(
        Guid id,
        SupportTicketInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await repository.GetByIdAsync(
            id,
            includeArchived: false,
            track: true,
            cancellationToken);
        if (ticket is null)
        {
            return SupportTicketOperationResult.NotFound();
        }

        if (ticket.Version != input.Version)
        {
            return SupportTicketOperationResult.Conflict(id);
        }

        SupportTicketReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        if (!TryNormalizeNumber(input.Number, out string normalizedNumber, out string? numberError))
        {
            return SupportTicketOperationResult.Invalid(numberError!);
        }

        string[] errors = Validate(input, data);
        if (errors.Length > 0)
        {
            return SupportTicketOperationResult.Invalid(errors);
        }

        if (await repository.IsNumberInUseAsync(normalizedNumber, id, cancellationToken))
        {
            return SupportTicketOperationResult.DuplicateNumber(id);
        }

        StatusDefinition newStatus = data.Statuses.Single(status => status.Id == input.StatusId);
        string oldNumber = ticket.TicketNumber;
        string oldTitle = ticket.Title;
        string? oldDescription = ticket.Description;
        string? oldPendingAction = ticket.PendingAction;
        string? oldNotes = ticket.Notes;
        Guid oldCategoryId = ticket.CategoryId;
        Guid oldAreaId = ticket.ResponsibleAreaId;
        Guid? oldPersonId = ticket.ResponsiblePersonId;
        Guid oldStatusId = ticket.StatusDefinitionId;
        string oldStatusName = ticket.StatusDefinition.Name;
        LifecycleState oldLifecycle = ticket.StatusDefinition.LifecycleState;
        PriorityLevel oldPriority = ticket.Priority;
        DateOnly oldOpenedOn = ticket.OpenedOn;
        DateOnly? oldDueDate = ticket.DueDate;
        DateTime now = GetUtcNow();

        ApplyInput(ticket, input, normalizedNumber);
        ticket.UpdatedAtUtc = now;
        ApplyLifecycle(ticket, newStatus.LifecycleState, now);

        if (oldStatusId != ticket.StatusDefinitionId)
        {
            AddHistory(
                id,
                actorUserId,
                ActivityActionType.StatusChanged,
                $"Status alterado de '{oldStatusName}' para '{newStatus.Name}'.",
                new { Before = oldStatusId, After = ticket.StatusDefinitionId });

            if (newStatus.LifecycleState == LifecycleState.Completed && oldLifecycle != LifecycleState.Completed)
            {
                AddHistory(id, actorUserId, ActivityActionType.Completed, $"Chamado {normalizedNumber} concluído.");
            }
            else if (newStatus.LifecycleState == LifecycleState.Active && oldLifecycle != LifecycleState.Active)
            {
                AddHistory(id, actorUserId, ActivityActionType.Reopened, $"Chamado {normalizedNumber} reaberto.");
            }
        }

        if (oldAreaId != ticket.ResponsibleAreaId || oldPersonId != ticket.ResponsiblePersonId)
        {
            AddHistory(
                id,
                actorUserId,
                ActivityActionType.ResponsibleChanged,
                "Equipe ou pessoa responsável alterada.",
                new
                {
                    AreaBefore = oldAreaId,
                    AreaAfter = ticket.ResponsibleAreaId,
                    PersonBefore = oldPersonId,
                    PersonAfter = ticket.ResponsiblePersonId,
                });
        }

        if (oldDueDate != ticket.DueDate)
        {
            AddHistory(
                id,
                actorUserId,
                ActivityActionType.DueDateChanged,
                "Prazo do chamado alterado.",
                new { Before = oldDueDate, After = ticket.DueDate });
        }

        if (oldNumber != ticket.TicketNumber
            || oldTitle != ticket.Title
            || oldDescription != ticket.Description
            || oldPendingAction != ticket.PendingAction
            || oldNotes != ticket.Notes
            || oldCategoryId != ticket.CategoryId
            || oldPriority != ticket.Priority
            || oldOpenedOn != ticket.OpenedOn)
        {
            AddHistory(id, actorUserId, ActivityActionType.Updated, "Dados do chamado atualizados.");
        }

        return await SaveOperationAsync(id, cancellationToken);
    }

    public async Task<SupportTicketOperationResult> ArchiveAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await repository.GetByIdAsync(
            id,
            includeArchived: false,
            track: true,
            cancellationToken);
        if (ticket is null)
        {
            return SupportTicketOperationResult.NotFound();
        }

        if (ticket.Version != version)
        {
            return SupportTicketOperationResult.Conflict(id);
        }

        DateTime now = GetUtcNow();
        ticket.ArchivedAtUtc = now;
        ticket.UpdatedAtUtc = now;
        AddHistory(id, actorUserId, ActivityActionType.Archived, $"Chamado {ticket.TicketNumber} arquivado.");
        return await SaveOperationAsync(id, cancellationToken);
    }

    public async Task<SupportTicketOperationResult> RestoreAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await repository.GetByIdAsync(
            id,
            includeArchived: true,
            track: true,
            cancellationToken);
        if (ticket is null || ticket.ArchivedAtUtc is null)
        {
            return SupportTicketOperationResult.NotFound();
        }

        if (ticket.Version != version)
        {
            return SupportTicketOperationResult.Conflict(id);
        }

        DateTime now = GetUtcNow();
        ticket.ArchivedAtUtc = null;
        ticket.UpdatedAtUtc = now;
        AddHistory(id, actorUserId, ActivityActionType.Restored, $"Chamado {ticket.TicketNumber} restaurado para o quadro.");
        return await SaveOperationAsync(id, cancellationToken);
    }

    private async Task<SupportTicketOperationResult> SaveOperationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return SupportTicketOperationResult.Success(id);
        }
        catch (DuplicateSupportTicketNumberException)
        {
            return SupportTicketOperationResult.DuplicateNumber(id);
        }
        catch (ConcurrencyConflictException)
        {
            return SupportTicketOperationResult.Conflict(id);
        }
    }

    private void AddHistory(
        Guid ticketId,
        Guid actorUserId,
        ActivityActionType action,
        string summary,
        object? changes = null)
    {
        repository.AddHistory(new ActivityHistory
        {
            Id = Guid.NewGuid(),
            EntityType = TrackedEntityType.SupportTicket,
            EntityId = ticketId,
            ActionType = action,
            OccurredAtUtc = GetUtcNow(),
            ActorUserId = actorUserId,
            Summary = summary,
            ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes),
        });
    }

    private static string[] Validate(SupportTicketInput input, SupportTicketReferenceData data)
    {
        var errors = new List<string>();
        if (input.Number.Trim().Length > 30)
        {
            errors.Add("O número deve ter no máximo 30 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            errors.Add("Informe o título do chamado.");
        }
        else if (input.Title.Trim().Length > 200)
        {
            errors.Add("O título deve ter no máximo 200 caracteres.");
        }

        AddLengthError(input.Description, 4000, "A descrição deve ter no máximo 4.000 caracteres.", errors);
        AddLengthError(input.PendingAction, 1000, "A ação pendente deve ter no máximo 1.000 caracteres.", errors);
        AddLengthError(input.Notes, 4000, "As observações devem ter no máximo 4.000 caracteres.", errors);

        if (!data.Categories.Any(category => category.Id == input.CategoryId))
        {
            errors.Add("Selecione uma categoria válida para chamados.");
        }

        if (!data.Areas.Any(area => area.Id == input.ResponsibleAreaId))
        {
            errors.Add("Selecione uma equipe responsável válida.");
        }

        if (!data.Statuses.Any(status => status.Id == input.StatusId))
        {
            errors.Add("Selecione um status válido para chamados.");
        }

        if (input.ResponsiblePersonId.HasValue && !data.People.Any(person => person.Id == input.ResponsiblePersonId))
        {
            errors.Add("Selecione uma pessoa responsável válida.");
        }

        if (input.OpenedOn == default)
        {
            errors.Add("Informe a data de abertura.");
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

    private static bool TryNormalizeNumber(
        string number,
        out string normalizedNumber,
        out string? error)
    {
        try
        {
            normalizedNumber = TicketNumberNormalizer.Normalize(number);
            error = null;
            return true;
        }
        catch (ArgumentException)
        {
            normalizedNumber = string.Empty;
            error = "Informe o número do chamado.";
            return false;
        }
        catch (FormatException exception)
        {
            normalizedNumber = string.Empty;
            error = exception.Message;
            return false;
        }
    }

    private static void ApplyInput(
        SupportTicket ticket,
        SupportTicketInput input,
        string normalizedNumber)
    {
        ticket.TicketNumber = normalizedNumber;
        ticket.NormalizedNumber = normalizedNumber;
        ticket.Title = input.Title.Trim();
        ticket.Description = NormalizeOptional(input.Description);
        ticket.CategoryId = input.CategoryId;
        ticket.ResponsibleAreaId = input.ResponsibleAreaId;
        ticket.ResponsiblePersonId = input.ResponsiblePersonId;
        ticket.StatusDefinitionId = input.StatusId;
        ticket.Priority = input.Priority;
        ticket.OpenedOn = input.OpenedOn;
        ticket.DueDate = input.DueDate;
        ticket.PendingAction = NormalizeOptional(input.PendingAction);
        ticket.Notes = NormalizeOptional(input.Notes);
    }

    private static void ApplyLifecycle(SupportTicket ticket, LifecycleState lifecycleState, DateTime now)
    {
        ticket.CompletedAtUtc = lifecycleState == LifecycleState.Completed
            ? ticket.CompletedAtUtc ?? now
            : null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StatusDefinition GetDefaultActiveStatus(SupportTicketReferenceData data) =>
        data.Statuses.FirstOrDefault(status => status.Code == "OPEN" && status.LifecycleState == LifecycleState.Active)
        ?? data.Statuses.First(status => status.LifecycleState == LifecycleState.Active);

    private static SupportTicketFormOptions MapFormOptions(SupportTicketReferenceData data) => new(
        data.Categories.Select(category => new SupportTicketLookupOption(
            category.Id,
            category.Name,
            category.ColorToken)).ToList(),
        data.Areas.Select(area => new SupportTicketLookupOption(area.Id, area.Name, area.ColorToken)).ToList(),
        data.People.Select(person => new SupportTicketLookupOption(
            person.Id,
            person.DisplayName,
            Group: person.TeamArea?.Name)).ToList(),
        data.Statuses.Select(status => new SupportTicketLookupOption(
            status.Id,
            status.Name,
            status.ColorToken)).ToList());

    private static SupportTicketFilterOptions MapFilterOptions(SupportTicketReferenceData data) => new(
        data.Categories.Select(category => new SupportTicketLookupOption(
            category.Id,
            category.Name,
            category.ColorToken)).ToList(),
        data.Areas.Select(area => new SupportTicketLookupOption(area.Id, area.Name, area.ColorToken)).ToList(),
        data.People.Select(person => new SupportTicketLookupOption(person.Id, person.DisplayName)).ToList(),
        data.Statuses.Select(status => new SupportTicketLookupOption(
            status.Id,
            status.Name,
            status.ColorToken)).ToList());

    private static SupportTicketBoardData MapBoard(
        IReadOnlyList<SupportTicket> tickets,
        SupportTicketReferenceData data,
        SupportTicketSearch search)
    {
        IEnumerable<Category> categories = data.Categories;
        if (search.CategoryId.HasValue)
        {
            categories = categories.Where(category => category.Id == search.CategoryId.Value);
        }

        List<SupportTicketBoardGroup> groups = categories
            .OrderBy(category => category.SortOrder)
            .Select(category => new SupportTicketBoardGroup(
                category.Id,
                category.Name,
                category.ColorToken,
                category.SortOrder,
                tickets.Where(ticket => ticket.CategoryId == category.Id)
                    .OrderByDescending(ticket => WorkItemDeadlineRules.IsOverdue(
                        ticket.DueDate,
                        ticket.StatusDefinition.LifecycleState,
                        ticket.ArchivedAtUtc,
                        search.Today))
                    .ThenBy(ticket => ticket.DueDate.HasValue ? 0 : 1)
                    .ThenBy(ticket => ticket.DueDate)
                    .ThenBy(ticket => ticket.NormalizedNumber)
                    .Select(ticket => MapCard(ticket, search.Today))
                    .ToList()))
            .ToList();

        return new SupportTicketBoardData(groups, MapFilterOptions(data), search.ArchivedOnly);
    }

    private static SupportTicketBoardCard MapCard(SupportTicket ticket, DateOnly today) => new(
        ticket.Id,
        ticket.TicketNumber,
        ticket.Title,
        ticket.Description,
        ticket.Category.Name,
        ticket.Category.ColorToken,
        ticket.ResponsibleArea.Name,
        ticket.ResponsibleArea.ColorToken,
        ticket.ResponsiblePerson?.DisplayName,
        ticket.StatusDefinition.Name,
        ticket.StatusDefinition.ColorToken,
        ticket.StatusDefinition.LifecycleState,
        ticket.Priority,
        ticket.OpenedOn,
        ticket.DueDate,
        ticket.PendingAction,
        WorkItemDeadlineRules.IsOverdue(
            ticket.DueDate,
            ticket.StatusDefinition.LifecycleState,
            ticket.ArchivedAtUtc,
            today),
        WorkItemDeadlineRules.IsDueSoon(
            ticket.DueDate,
            ticket.StatusDefinition.LifecycleState,
            ticket.ArchivedAtUtc,
            today),
        SupportTicketOperationalRules.RequiresAction(
            ticket.PendingAction,
            ticket.StatusDefinition.LifecycleState,
            ticket.ArchivedAtUtc),
        ticket.Version);

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
