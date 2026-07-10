using System.Text.Json;
using CentraSA.Application.Common;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;

namespace CentraSA.Application.PendingTasks;

public sealed class PendingTaskService(
    IPendingTaskRepository repository,
    TimeProvider timeProvider) : IPendingTaskService
{
    public async Task<PendingTaskListData> SearchAsync(
        PendingTaskSearch search,
        CancellationToken cancellationToken = default)
    {
        PendingTaskPage page = await repository.SearchAsync(search, cancellationToken);
        PendingTaskReferenceData referenceData = await repository.GetReferenceDataAsync(cancellationToken);
        return MapListData(page, referenceData, search.Today, search.ArchivedOnly);
    }

    public Task<PendingTaskListData> GetPresentationAsync(
        bool showCompleted,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = GetToday();
        return SearchAsync(
            new PendingTaskSearch(
                Search: null,
                AreaId: null,
                PersonId: null,
                StatusId: null,
                DueFilter: PendingTaskDueFilter.All,
                HideCompleted: !showCompleted,
                ArchivedOnly: false,
                Today: today,
                PageSize: 5000),
            cancellationToken);
    }

    public async Task<PendingTaskEditorData> GetCreateEditorAsync(CancellationToken cancellationToken = default)
    {
        PendingTaskReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        StatusDefinition defaultStatus = GetDefaultActiveStatus(data);
        Category? defaultCategory = data.Categories.FirstOrDefault(category => category.Code == "OPERATIONAL")
            ?? (data.Categories.Count > 0 ? data.Categories[0] : null);
        var input = new PendingTaskInput
        {
            ResponsibleAreaId = data.Areas.Count > 0 ? data.Areas[0].Id : Guid.Empty,
            StatusId = defaultStatus.Id,
            CategoryId = defaultCategory?.Id,
            Priority = PriorityLevel.Medium,
            Version = 1,
        };
        return new PendingTaskEditorData(null, input, MapFormOptions(data));
    }

    public async Task<PendingTaskEditorData?> GetEditEditorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        PendingTask? task = await repository.GetByIdAsync(id, includeArchived: false, track: false, cancellationToken);
        if (task is null)
        {
            return null;
        }

        PendingTaskReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        WorkItemReference? reference = task.References.FirstOrDefault();
        var input = new PendingTaskInput
        {
            Title = task.Title,
            Description = task.Description,
            ResponsibleAreaId = task.ResponsibleAreaId,
            ResponsiblePersonId = task.ResponsiblePersonId,
            StatusId = task.StatusDefinitionId,
            CategoryId = task.CategoryId,
            Priority = task.Priority,
            DueDate = task.DueDate,
            Origin = task.Origin,
            Notes = task.Notes,
            RelatedSmudId = reference?.SmudId,
            RelatedSupportTicketId = reference?.SupportTicketId,
            Version = task.Version,
        };
        return new PendingTaskEditorData(task.Id, input, MapFormOptions(data));
    }

    public async Task<PendingTaskDetailsData?> GetDetailsAsync(
        Guid id,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        PendingTask? task = await repository.GetByIdAsync(id, includeArchived, track: false, cancellationToken);
        if (task is null)
        {
            return null;
        }

        IReadOnlyList<ActivityHistory> history = await repository.GetHistoryAsync(id, cancellationToken);
        DateOnly today = GetToday();
        return new PendingTaskDetailsData(
            MapListItem(task, today),
            task.Origin,
            task.Notes,
            task.CreatedAtUtc,
            task.UpdatedAtUtc,
            task.ArchivedAtUtc,
            history.Select(item => new ActivityHistoryItem(
                TranslateAction(item.ActionType),
                item.Summary,
                item.OccurredAtUtc)).ToList());
    }

    public async Task<PendingTaskOperationResult> CreateAsync(
        PendingTaskInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        PendingTaskReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        return await CreateCoreAsync(input, actorUserId, data, cancellationToken);
    }

    public async Task<PendingTaskOperationResult> QuickCreateAsync(
        PendingTaskQuickInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        PendingTaskReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        StatusDefinition defaultStatus = GetDefaultActiveStatus(data);
        Category? defaultCategory = data.Categories.FirstOrDefault(category => category.Code == "OPERATIONAL")
            ?? (data.Categories.Count > 0 ? data.Categories[0] : null);
        var fullInput = new PendingTaskInput
        {
            Title = input.Title,
            ResponsibleAreaId = input.ResponsibleAreaId,
            StatusId = defaultStatus.Id,
            CategoryId = defaultCategory?.Id,
            Priority = PriorityLevel.Medium,
            DueDate = input.DueDate,
            Origin = "Criação rápida",
        };
        return await CreateCoreAsync(fullInput, actorUserId, data, cancellationToken);
    }

    public async Task<PendingTaskOperationResult> UpdateAsync(
        Guid id,
        PendingTaskInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        PendingTask? task = await repository.GetByIdAsync(id, includeArchived: false, track: true, cancellationToken);
        if (task is null)
        {
            return PendingTaskOperationResult.NotFound();
        }

        if (task.Version != input.Version)
        {
            return PendingTaskOperationResult.Conflict(id);
        }

        PendingTaskReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        string[] errors = Validate(input, data);
        if (errors.Length > 0)
        {
            return PendingTaskOperationResult.Invalid(errors);
        }

        StatusDefinition newStatus = data.Statuses.Single(status => status.Id == input.StatusId);
        string oldTitle = task.Title;
        string? oldDescription = task.Description;
        string? oldOrigin = task.Origin;
        string? oldNotes = task.Notes;
        Guid? oldCategoryId = task.CategoryId;
        PriorityLevel oldPriority = task.Priority;
        Guid oldAreaId = task.ResponsibleAreaId;
        Guid? oldPersonId = task.ResponsiblePersonId;
        DateOnly? oldDueDate = task.DueDate;
        Guid oldStatusId = task.StatusDefinitionId;
        string oldStatusName = task.StatusDefinition.Name;
        LifecycleState oldLifecycle = task.StatusDefinition.LifecycleState;
        DateTime now = GetUtcNow();

        ApplyInput(task, input);
        task.UpdatedAtUtc = now;
        ApplyLifecycle(task, newStatus.LifecycleState, now);

        if (oldStatusId != task.StatusDefinitionId)
        {
            AddHistory(task.Id, actorUserId, ActivityActionType.StatusChanged,
                $"Status alterado de '{oldStatusName}' para '{newStatus.Name}'.",
                new { Before = oldStatusId, After = task.StatusDefinitionId });

            if (newStatus.LifecycleState == LifecycleState.Completed && oldLifecycle != LifecycleState.Completed)
            {
                AddHistory(task.Id, actorUserId, ActivityActionType.Completed, "Pendência concluída.");
            }
            else if (newStatus.LifecycleState == LifecycleState.Active && oldLifecycle != LifecycleState.Active)
            {
                AddHistory(task.Id, actorUserId, ActivityActionType.Reopened, "Pendência reaberta.");
            }
        }

        if (oldAreaId != task.ResponsibleAreaId || oldPersonId != task.ResponsiblePersonId)
        {
            AddHistory(task.Id, actorUserId, ActivityActionType.ResponsibleChanged,
                "Área ou pessoa responsável alterada.",
                new { AreaBefore = oldAreaId, AreaAfter = task.ResponsibleAreaId, PersonBefore = oldPersonId, PersonAfter = task.ResponsiblePersonId });
        }

        if (oldDueDate != task.DueDate)
        {
            AddHistory(task.Id, actorUserId, ActivityActionType.DueDateChanged,
                "Prazo da pendência alterado.",
                new { Before = oldDueDate, After = task.DueDate });
        }

        if (oldTitle != task.Title
            || oldDescription != task.Description
            || oldOrigin != task.Origin
            || oldNotes != task.Notes
            || oldCategoryId != task.CategoryId
            || oldPriority != task.Priority)
        {
            AddHistory(task.Id, actorUserId, ActivityActionType.Updated, "Dados da pendência atualizados.");
        }

        await repository.ReplaceReferenceAsync(
            task.Id,
            input.RelatedSmudId,
            input.RelatedSupportTicketId,
            cancellationToken);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return PendingTaskOperationResult.Success(task.Id);
        }
        catch (ConcurrencyConflictException)
        {
            return PendingTaskOperationResult.Conflict(task.Id);
        }
    }

    public Task<PendingTaskOperationResult> CompleteAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(id, version, actorUserId, LifecycleState.Completed, cancellationToken);

    public Task<PendingTaskOperationResult> ReopenAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleAsync(id, version, actorUserId, LifecycleState.Active, cancellationToken);

    public async Task<PendingTaskOperationResult> ArchiveAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        PendingTask? task = await repository.GetByIdAsync(id, includeArchived: false, track: true, cancellationToken);
        if (task is null)
        {
            return PendingTaskOperationResult.NotFound();
        }

        if (task.Version != version)
        {
            return PendingTaskOperationResult.Conflict(id);
        }

        DateTime now = GetUtcNow();
        task.ArchivedAtUtc = now;
        task.UpdatedAtUtc = now;
        AddHistory(id, actorUserId, ActivityActionType.Archived, "Pendência arquivada.");
        return await SaveOperationAsync(task.Id, cancellationToken);
    }

    public async Task<PendingTaskOperationResult> RestoreAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        PendingTask? task = await repository.GetByIdAsync(id, includeArchived: true, track: true, cancellationToken);
        if (task is null || task.ArchivedAtUtc is null)
        {
            return PendingTaskOperationResult.NotFound();
        }

        if (task.Version != version)
        {
            return PendingTaskOperationResult.Conflict(id);
        }

        DateTime now = GetUtcNow();
        task.ArchivedAtUtc = null;
        task.UpdatedAtUtc = now;
        AddHistory(id, actorUserId, ActivityActionType.Restored, "Pendência restaurada para as telas principais.");
        return await SaveOperationAsync(task.Id, cancellationToken);
    }

    public async Task<PendingTaskOperationResult> MoveAsync(
        Guid id,
        long version,
        PendingTaskMoveDirection direction,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingTask> orderedTasks = await repository.GetOrderedActiveAsync(cancellationToken);
        int currentIndex = orderedTasks.ToList().FindIndex(task => task.Id == id);
        if (currentIndex < 0)
        {
            return PendingTaskOperationResult.NotFound();
        }

        PendingTask current = orderedTasks[currentIndex];
        if (current.Version != version)
        {
            return PendingTaskOperationResult.Conflict(id);
        }

        int targetIndex = direction == PendingTaskMoveDirection.Up ? currentIndex - 1 : currentIndex + 1;
        if (targetIndex < 0 || targetIndex >= orderedTasks.Count)
        {
            return PendingTaskOperationResult.Success(id);
        }

        PendingTask target = orderedTasks[targetIndex];
        (current.PresentationOrder, target.PresentationOrder) = (target.PresentationOrder, current.PresentationOrder);
        DateTime now = GetUtcNow();
        current.UpdatedAtUtc = now;
        target.UpdatedAtUtc = now;
        AddHistory(id, actorUserId, ActivityActionType.Updated, "Ordem de apresentação alterada.");
        return await SaveOperationAsync(id, cancellationToken);
    }

    private async Task<PendingTaskOperationResult> CreateCoreAsync(
        PendingTaskInput input,
        Guid actorUserId,
        PendingTaskReferenceData data,
        CancellationToken cancellationToken)
    {
        string[] errors = Validate(input, data);
        if (errors.Length > 0)
        {
            return PendingTaskOperationResult.Invalid(errors);
        }

        StatusDefinition status = data.Statuses.Single(item => item.Id == input.StatusId);
        DateTime now = GetUtcNow();
        var task = new PendingTask
        {
            Id = Guid.NewGuid(),
            PresentationOrder = await repository.GetNextPresentationOrderAsync(cancellationToken),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        ApplyInput(task, input);
        ApplyLifecycle(task, status.LifecycleState, now);

        repository.Add(task);
        AddHistory(task.Id, actorUserId, ActivityActionType.Created, "Pendência criada.");
        await repository.ReplaceReferenceAsync(task.Id, input.RelatedSmudId, input.RelatedSupportTicketId, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return PendingTaskOperationResult.Success(task.Id);
    }

    private async Task<PendingTaskOperationResult> ChangeLifecycleAsync(
        Guid id,
        long version,
        Guid actorUserId,
        LifecycleState targetLifecycle,
        CancellationToken cancellationToken)
    {
        PendingTask? task = await repository.GetByIdAsync(id, includeArchived: false, track: true, cancellationToken);
        if (task is null)
        {
            return PendingTaskOperationResult.NotFound();
        }

        if (task.Version != version)
        {
            return PendingTaskOperationResult.Conflict(id);
        }

        if (task.StatusDefinition.LifecycleState == targetLifecycle)
        {
            return PendingTaskOperationResult.Success(id);
        }

        PendingTaskReferenceData data = await repository.GetReferenceDataAsync(cancellationToken);
        StatusDefinition targetStatus = targetLifecycle == LifecycleState.Active
            ? GetDefaultActiveStatus(data)
            : data.Statuses.First(status => status.LifecycleState == targetLifecycle);
        DateTime now = GetUtcNow();
        string previousStatus = task.StatusDefinition.Name;
        task.StatusDefinitionId = targetStatus.Id;
        task.UpdatedAtUtc = now;
        ApplyLifecycle(task, targetLifecycle, now);

        ActivityActionType action = targetLifecycle == LifecycleState.Completed
            ? ActivityActionType.Completed
            : ActivityActionType.Reopened;
        string summary = targetLifecycle == LifecycleState.Completed ? "Pendência concluída." : "Pendência reaberta.";
        AddHistory(id, actorUserId, action, summary, new { Before = previousStatus, After = targetStatus.Name });
        return await SaveOperationAsync(id, cancellationToken);
    }

    private async Task<PendingTaskOperationResult> SaveOperationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return PendingTaskOperationResult.Success(id);
        }
        catch (ConcurrencyConflictException)
        {
            return PendingTaskOperationResult.Conflict(id);
        }
    }

    private void AddHistory(
        Guid taskId,
        Guid actorUserId,
        ActivityActionType action,
        string summary,
        object? changes = null)
    {
        repository.AddHistory(new ActivityHistory
        {
            Id = Guid.NewGuid(),
            EntityType = TrackedEntityType.PendingTask,
            EntityId = taskId,
            ActionType = action,
            OccurredAtUtc = GetUtcNow(),
            ActorUserId = actorUserId,
            Summary = summary,
            ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes),
        });
    }

    private static string[] Validate(PendingTaskInput input, PendingTaskReferenceData data)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            errors.Add("Informe o título da pendência.");
        }
        else if (input.Title.Trim().Length > 200)
        {
            errors.Add("O título deve ter no máximo 200 caracteres.");
        }

        if (!data.Areas.Any(area => area.Id == input.ResponsibleAreaId))
        {
            errors.Add("Selecione uma área responsável válida.");
        }

        if (!data.Statuses.Any(status => status.Id == input.StatusId))
        {
            errors.Add("Selecione um status válido para pendências.");
        }

        if (input.ResponsiblePersonId.HasValue && !data.People.Any(person => person.Id == input.ResponsiblePersonId))
        {
            errors.Add("Selecione uma pessoa responsável válida.");
        }

        if (input.CategoryId.HasValue && !data.Categories.Any(category => category.Id == input.CategoryId))
        {
            errors.Add("Selecione uma categoria válida.");
        }

        if (input.RelatedSmudId.HasValue && input.RelatedSupportTicketId.HasValue)
        {
            errors.Add("Relacione a pendência a um SMUD ou a um chamado, não aos dois.");
        }
        else if (input.RelatedSmudId.HasValue && !data.Smuds.Any(smud => smud.Id == input.RelatedSmudId))
        {
            errors.Add("O SMUD relacionado não está disponível.");
        }
        else if (input.RelatedSupportTicketId.HasValue && !data.SupportTickets.Any(ticket => ticket.Id == input.RelatedSupportTicketId))
        {
            errors.Add("O chamado relacionado não está disponível.");
        }

        return errors.ToArray();
    }

    private static void ApplyInput(PendingTask task, PendingTaskInput input)
    {
        task.Title = input.Title.Trim();
        task.Description = NormalizeOptional(input.Description);
        task.ResponsibleAreaId = input.ResponsibleAreaId;
        task.ResponsiblePersonId = input.ResponsiblePersonId;
        task.StatusDefinitionId = input.StatusId;
        task.CategoryId = input.CategoryId;
        task.Priority = input.Priority;
        task.DueDate = input.DueDate;
        task.Origin = NormalizeOptional(input.Origin);
        task.Notes = NormalizeOptional(input.Notes);
    }

    private static void ApplyLifecycle(PendingTask task, LifecycleState lifecycleState, DateTime now)
    {
        task.CompletedAtUtc = lifecycleState == LifecycleState.Completed ? task.CompletedAtUtc ?? now : null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StatusDefinition GetDefaultActiveStatus(PendingTaskReferenceData data) =>
        data.Statuses.FirstOrDefault(status => status.Code == "OPEN" && status.LifecycleState == LifecycleState.Active)
        ?? data.Statuses.First(status => status.LifecycleState == LifecycleState.Active);

    private static PendingTaskFormOptions MapFormOptions(PendingTaskReferenceData data) => new(
        data.Areas.Select(area => new LookupOption(area.Id, area.Name, area.ColorToken)).ToList(),
        data.People.Select(person => new LookupOption(person.Id, person.DisplayName, Group: person.TeamArea?.Name)).ToList(),
        data.Statuses.Select(status => new LookupOption(status.Id, status.Name, status.ColorToken)).ToList(),
        data.Categories.Select(category => new LookupOption(category.Id, category.Name, category.ColorToken)).ToList(),
        data.Smuds.Select(smud => new LookupOption(smud.Id, $"{smud.Code} — {smud.Title}")).ToList(),
        data.SupportTickets.Select(ticket => new LookupOption(ticket.Id, $"{ticket.TicketNumber} — {ticket.Title}")).ToList());

    private static PendingTaskFilterOptions MapFilterOptions(PendingTaskReferenceData data) => new(
        data.Areas.Select(area => new LookupOption(area.Id, area.Name, area.ColorToken)).ToList(),
        data.People.Select(person => new LookupOption(person.Id, person.DisplayName)).ToList(),
        data.Statuses.Select(status => new LookupOption(status.Id, status.Name, status.ColorToken)).ToList());

    private static PendingTaskListData MapListData(
        PendingTaskPage page,
        PendingTaskReferenceData data,
        DateOnly today,
        bool archivedOnly) => new(
        page.Items.Select(task => MapListItem(task, today)).ToList(),
        MapFilterOptions(data),
        page.TotalCount,
        page.Page,
        page.PageSize,
        archivedOnly);

    private static PendingTaskListItem MapListItem(PendingTask task, DateOnly today)
    {
        WorkItemReference? reference = task.References.FirstOrDefault();
        string? relatedItem = reference?.Smud is not null
            ? reference.Smud.Code
            : reference?.SupportTicket is not null
                ? $"Chamado {reference.SupportTicket.TicketNumber}"
                : null;

        return new PendingTaskListItem(
            task.Id,
            task.Title,
            task.Description,
            task.ResponsibleArea.Name,
            task.ResponsiblePerson?.DisplayName,
            task.Category?.Name,
            task.StatusDefinition.Name,
            task.StatusDefinition.ColorToken,
            task.Priority,
            task.DueDate,
            task.CompletedAtUtc,
            WorkItemDeadlineRules.IsOverdue(task.DueDate, task.StatusDefinition.LifecycleState, task.ArchivedAtUtc, today),
            WorkItemDeadlineRules.IsDueSoon(task.DueDate, task.StatusDefinition.LifecycleState, task.ArchivedAtUtc, today),
            task.PresentationOrder,
            task.Version,
            relatedItem);
    }

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
