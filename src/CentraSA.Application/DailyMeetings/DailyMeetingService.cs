using CentraSA.Application.Common;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;

namespace CentraSA.Application.DailyMeetings;

public sealed class DailyMeetingService(
    IDailyMeetingRepository repository,
    TimeProvider timeProvider) : IDailyMeetingService
{
    public async Task<DailyMeetingOverviewData> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DailyMeeting> meetings = await repository.ListAsync(cancellationToken);
        DailyMeeting? latest = await repository.GetLatestAsync(cancellationToken);
        return new DailyMeetingOverviewData(
            meetings.Select(MapSummary).ToList(),
            latest is null ? null : MapSummary(latest));
    }

    public async Task<DailyMeetingBuilderData> GetCreateBuilderAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MeetingSourceCandidate> candidates = await GetCandidatesAsync(cancellationToken);
        DateOnly today = GetToday();
        List<DailyMeetingBuilderRow> rows = candidates
            .Select(candidate => CreateCandidateRow(candidate, today))
            .OrderBy(row => SectionRank(row.RecommendedSection))
            .ThenBy(row => row.SortOrder)
            .ThenBy(row => row.Title)
            .Select((row, index) => row with { SortOrder = (index + 1) * 10 })
            .ToList();

        return new DailyMeetingBuilderData(
            Id: null,
            MeetingDate: today,
            GeneralNotes: null,
            Version: 1,
            Rows: rows);
    }

    public async Task<DailyMeetingBuilderData?> GetEditBuilderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        DailyMeeting? meeting = await repository.GetByIdAsync(id, track: false, cancellationToken);
        if (meeting is null || meeting.Status == MeetingStatus.Finished)
        {
            return null;
        }

        IReadOnlyList<MeetingSourceCandidate> candidates = await GetCandidatesAsync(cancellationToken);
        var candidateLookup = candidates.ToDictionary(candidate => SourceKey(candidate.SourceType, candidate.SourceId));
        var selectedKeys = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<DailyMeetingBuilderRow>();

        foreach (DailyMeetingItem item in meeting.Items.OrderBy(item => SectionRank(item.Section)).ThenBy(item => item.SortOrder))
        {
            (TrackedEntityType sourceType, Guid sourceId) = GetSource(item);
            string key = SourceKey(sourceType, sourceId);
            selectedKeys.Add(key);
            candidateLookup.TryGetValue(key, out MeetingSourceCandidate? candidate);
            rows.Add(new DailyMeetingBuilderRow(
                item.Id,
                sourceType,
                sourceId,
                SourceLabel(sourceType),
                item.SnapshotTitle,
                candidate?.Status ?? item.SnapshotStatus,
                item.SnapshotDueDate,
                item.SnapshotResponsible,
                Selected: true,
                RecommendedSection: candidate is null ? item.Section : RecommendSection(candidate, GetToday()),
                Section: item.Section,
                SortOrder: item.SortOrder,
                PresentationNotes: item.PresentationNotes,
                SuggestionReason: "Já selecionado para este roteiro"));
        }

        rows.AddRange(candidates
            .Where(candidate => !selectedKeys.Contains(SourceKey(candidate.SourceType, candidate.SourceId)))
            .Select(candidate => CreateCandidateRow(candidate, GetToday()))
            .OrderBy(row => SectionRank(row.RecommendedSection))
            .ThenBy(row => row.SortOrder)
            .ThenBy(row => row.Title));

        return new DailyMeetingBuilderData(
            meeting.Id,
            meeting.MeetingDate,
            meeting.GeneralNotes,
            meeting.Version,
            rows);
    }

    public async Task<DailyMeetingDetailsData?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        DailyMeeting? meeting = await repository.GetByIdAsync(id, track: false, cancellationToken);
        return meeting is null ? null : MapDetails(meeting);
    }

    public async Task<DailyMeetingOperationResult> CreateDraftAsync(
        DailyMeetingInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DailyMeetingSelectionInput> selections = GetSelected(input);
        string[] errors = ValidateInput(input, selections);
        if (errors.Length > 0)
        {
            return DailyMeetingOperationResult.Invalid(errors);
        }

        IReadOnlyList<MeetingSourceCandidate> candidates = await GetCandidatesAsync(cancellationToken);
        var lookup = candidates.ToDictionary(candidate => SourceKey(candidate.SourceType, candidate.SourceId));
        if (selections.Any(selection => !lookup.ContainsKey(SourceKey(selection.SourceType, selection.SourceId))))
        {
            return DailyMeetingOperationResult.Invalid("Uma das sugestões selecionadas não está mais disponível.");
        }

        DateTime now = GetUtcNow();
        var meeting = new DailyMeeting
        {
            Id = Guid.NewGuid(),
            MeetingDate = input.MeetingDate,
            StartedAtUtc = now,
            Status = MeetingStatus.Draft,
            GeneralNotes = NormalizeOptional(input.GeneralNotes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        AddSelectedItems(meeting, selections, lookup);
        repository.Add(meeting);
        AddMeetingHistory(meeting.Id, actorUserId, ActivityActionType.Created, "Rascunho da reunião diária criado.");
        return await SaveOperationAsync(meeting.Id, cancellationToken);
    }

    public async Task<DailyMeetingOperationResult> UpdateDraftAsync(
        Guid id,
        DailyMeetingInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        DailyMeeting? meeting = await repository.GetByIdAsync(id, track: true, cancellationToken);
        if (meeting is null)
        {
            return DailyMeetingOperationResult.NotFound();
        }

        DailyMeetingOperationResult? stateError = ValidateMutable(meeting, input.Version);
        if (stateError is not null)
        {
            return stateError;
        }

        IReadOnlyList<DailyMeetingSelectionInput> selections = GetSelected(input);
        string[] errors = ValidateInput(input, selections);
        if (errors.Length > 0)
        {
            return DailyMeetingOperationResult.Invalid(errors);
        }

        IReadOnlyList<MeetingSourceCandidate> candidates = await GetCandidatesAsync(cancellationToken);
        var candidateLookup = candidates.ToDictionary(candidate => SourceKey(candidate.SourceType, candidate.SourceId));
        var existingLookup = meeting.Items.ToDictionary(
            item =>
            {
                (TrackedEntityType sourceType, Guid sourceId) = GetSource(item);
                return SourceKey(sourceType, sourceId);
            });

        foreach (DailyMeetingSelectionInput selection in selections)
        {
            string key = SourceKey(selection.SourceType, selection.SourceId);
            if (!existingLookup.ContainsKey(key) && !candidateLookup.ContainsKey(key))
            {
                return DailyMeetingOperationResult.Invalid("Uma das sugestões selecionadas não está mais disponível.");
            }
        }

        var selectedKeys = selections
            .Select(selection => SourceKey(selection.SourceType, selection.SourceId))
            .ToHashSet(StringComparer.Ordinal);
        foreach ((string key, DailyMeetingItem item) in existingLookup)
        {
            if (!selectedKeys.Contains(key))
            {
                repository.RemoveItem(item);
            }
        }

        int order = 10;
        foreach (DailyMeetingSelectionInput selection in selections.OrderBy(item => item.SortOrder))
        {
            string key = SourceKey(selection.SourceType, selection.SourceId);
            if (existingLookup.TryGetValue(key, out DailyMeetingItem? existing))
            {
                existing.Section = selection.Section;
                existing.SortOrder = order;
                existing.PresentationNotes = NormalizeOptional(selection.PresentationNotes);
            }
            else
            {
                meeting.Items.Add(CreateItem(meeting.Id, selection, candidateLookup[key], order));
            }

            order += 10;
        }

        meeting.MeetingDate = input.MeetingDate;
        meeting.GeneralNotes = NormalizeOptional(input.GeneralNotes);
        meeting.UpdatedAtUtc = GetUtcNow();
        AddMeetingHistory(id, actorUserId, ActivityActionType.Updated, "Preparação, ordem ou notas da reunião atualizadas.");
        return await SaveOperationAsync(id, cancellationToken);
    }

    public Task<DailyMeetingOperationResult> MarkPresentedAsync(
        Guid id,
        Guid itemId,
        long version,
        bool wasPresented,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        UpdateItemAsync(
            id,
            itemId,
            version,
            actorUserId,
            item => item.WasPresented = wasPresented,
            wasPresented ? "Item marcado como apresentado." : "Item marcado como não apresentado.",
            cancellationToken);

    public Task<DailyMeetingOperationResult> UpdateItemNotesAsync(
        Guid id,
        Guid itemId,
        long version,
        string? notes,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (notes?.Trim().Length > 2000)
        {
            return Task.FromResult(DailyMeetingOperationResult.Invalid(
                "As notas do item devem ter no máximo 2.000 caracteres."));
        }

        return UpdateItemAsync(
            id,
            itemId,
            version,
            actorUserId,
            item => item.PresentationNotes = NormalizeOptional(notes),
            "Notas de apresentação do item atualizadas.",
            cancellationToken);
    }

    public async Task<DailyMeetingOperationResult> CompleteOriginalAsync(
        Guid id,
        Guid itemId,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        DailyMeeting? meeting = await repository.GetByIdAsync(id, track: true, cancellationToken);
        if (meeting is null)
        {
            return DailyMeetingOperationResult.NotFound();
        }

        DailyMeetingOperationResult? stateError = ValidateMutable(meeting, version);
        if (stateError is not null)
        {
            return stateError;
        }

        DailyMeetingItem? item = meeting.Items.SingleOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return DailyMeetingOperationResult.NotFound();
        }

        DateTime now = GetUtcNow();
        if (item.PendingTask is not null)
        {
            await CompletePendingTaskAsync(item.PendingTask, actorUserId, now, cancellationToken);
        }
        else if (item.Smud is not null)
        {
            await CompleteSmudAsync(item.Smud, actorUserId, now, cancellationToken);
        }
        else if (item.SupportTicket is not null)
        {
            await CompleteSupportTicketAsync(item.SupportTicket, actorUserId, now, cancellationToken);
        }

        item.WasPresented = true;
        meeting.UpdatedAtUtc = now;
        AddMeetingHistory(id, actorUserId, ActivityActionType.Updated, $"Item '{item.SnapshotTitle}' concluído durante a reunião.");
        return await SaveOperationAsync(id, cancellationToken);
    }

    public async Task<DailyMeetingOperationResult> FinishAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        DailyMeeting? meeting = await repository.GetByIdAsync(id, track: true, cancellationToken);
        if (meeting is null)
        {
            return DailyMeetingOperationResult.NotFound();
        }

        DailyMeetingOperationResult? stateError = ValidateMutable(meeting, version);
        if (stateError is not null)
        {
            return stateError;
        }

        DateTime now = GetUtcNow();
        meeting.Status = MeetingStatus.Finished;
        meeting.FinishedAtUtc = now;
        meeting.UpdatedAtUtc = now;
        AddMeetingHistory(id, actorUserId, ActivityActionType.Completed, "Reunião diária finalizada.");
        return await SaveOperationAsync(id, cancellationToken);
    }

    private async Task<DailyMeetingOperationResult> UpdateItemAsync(
        Guid id,
        Guid itemId,
        long version,
        Guid actorUserId,
        Action<DailyMeetingItem> update,
        string historySummary,
        CancellationToken cancellationToken)
    {
        DailyMeeting? meeting = await repository.GetByIdAsync(id, track: true, cancellationToken);
        if (meeting is null)
        {
            return DailyMeetingOperationResult.NotFound();
        }

        DailyMeetingOperationResult? stateError = ValidateMutable(meeting, version);
        if (stateError is not null)
        {
            return stateError;
        }

        DailyMeetingItem? item = meeting.Items.SingleOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            return DailyMeetingOperationResult.NotFound();
        }

        update(item);
        meeting.UpdatedAtUtc = GetUtcNow();
        AddMeetingHistory(id, actorUserId, ActivityActionType.Updated, historySummary);
        return await SaveOperationAsync(id, cancellationToken);
    }

    private async Task CompletePendingTaskAsync(
        PendingTask task,
        Guid actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (task.StatusDefinition.LifecycleState == LifecycleState.Completed)
        {
            return;
        }

        StatusDefinition status = await repository.GetCompletedStatusAsync(WorkItemScope.PendingTask, cancellationToken);
        task.StatusDefinitionId = status.Id;
        task.CompletedAtUtc = now;
        task.UpdatedAtUtc = now;
        AddSourceHistory(TrackedEntityType.PendingTask, task.Id, actorUserId, $"Pendência concluída durante a reunião diária.");
    }

    private async Task CompleteSmudAsync(
        Smud smud,
        Guid actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (smud.StatusDefinition.LifecycleState == LifecycleState.Completed)
        {
            return;
        }

        StatusDefinition status = await repository.GetCompletedStatusAsync(WorkItemScope.Smud, cancellationToken);
        smud.StatusDefinitionId = status.Id;
        smud.CompletedAtUtc = now;
        smud.UpdatedAtUtc = now;
        AddSourceHistory(TrackedEntityType.Smud, smud.Id, actorUserId, $"{smud.Code} concluído durante a reunião diária.");
    }

    private async Task CompleteSupportTicketAsync(
        SupportTicket ticket,
        Guid actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (ticket.StatusDefinition.LifecycleState == LifecycleState.Completed)
        {
            return;
        }

        StatusDefinition status = await repository.GetCompletedStatusAsync(WorkItemScope.SupportTicket, cancellationToken);
        ticket.StatusDefinitionId = status.Id;
        ticket.CompletedAtUtc = now;
        ticket.UpdatedAtUtc = now;
        AddSourceHistory(TrackedEntityType.SupportTicket, ticket.Id, actorUserId, $"Chamado {ticket.TicketNumber} concluído durante a reunião diária.");
    }

    private async Task<DailyMeetingOperationResult> SaveOperationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return DailyMeetingOperationResult.Success(id);
        }
        catch (ConcurrencyConflictException)
        {
            return DailyMeetingOperationResult.Conflict(id);
        }
    }

    private static void AddSelectedItems(
        DailyMeeting meeting,
        IReadOnlyList<DailyMeetingSelectionInput> selections,
        IReadOnlyDictionary<string, MeetingSourceCandidate> candidates)
    {
        int order = 10;
        foreach (DailyMeetingSelectionInput selection in selections.OrderBy(item => item.SortOrder))
        {
            string key = SourceKey(selection.SourceType, selection.SourceId);
            meeting.Items.Add(CreateItem(meeting.Id, selection, candidates[key], order));
            order += 10;
        }
    }

    private static DailyMeetingItem CreateItem(
        Guid meetingId,
        DailyMeetingSelectionInput selection,
        MeetingSourceCandidate candidate,
        int order)
    {
        var item = new DailyMeetingItem
        {
            Id = Guid.NewGuid(),
            DailyMeetingId = meetingId,
            Section = selection.Section,
            SortOrder = order,
            PresentationNotes = NormalizeOptional(selection.PresentationNotes),
            SnapshotTitle = candidate.Title,
            SnapshotStatus = candidate.Status,
            SnapshotDueDate = candidate.DueDate,
            SnapshotResponsible = candidate.Responsible,
        };

        switch (selection.SourceType)
        {
            case TrackedEntityType.PendingTask:
                item.PendingTaskId = selection.SourceId;
                break;
            case TrackedEntityType.Smud:
                item.SmudId = selection.SourceId;
                break;
            case TrackedEntityType.SupportTicket:
                item.SupportTicketId = selection.SourceId;
                break;
            default:
                throw new InvalidOperationException("Tipo de origem inválido para a reunião.");
        }

        return item;
    }

    private static List<DailyMeetingSelectionInput> GetSelected(DailyMeetingInput input) =>
        input.Items.Where(item => item.Selected).ToList();

    private static string[] ValidateInput(
        DailyMeetingInput input,
        IReadOnlyList<DailyMeetingSelectionInput> selections)
    {
        var errors = new List<string>();
        if (input.MeetingDate == default)
        {
            errors.Add("Informe a data da reunião.");
        }

        if (input.GeneralNotes?.Trim().Length > 4000)
        {
            errors.Add("As notas gerais devem ter no máximo 4.000 caracteres.");
        }

        if (selections.Count == 0)
        {
            errors.Add("Selecione ao menos um item para a reunião.");
        }

        if (selections.Any(item => !IsMeetingSource(item.SourceType)))
        {
            errors.Add("Uma seleção possui um tipo de origem inválido.");
        }

        if (selections.Any(item => !Enum.IsDefined(item.Section)))
        {
            errors.Add("Uma seleção possui uma seção inválida.");
        }

        if (selections.Any(item => item.PresentationNotes?.Trim().Length > 2000))
        {
            errors.Add("As notas de cada item devem ter no máximo 2.000 caracteres.");
        }

        bool hasDuplicate = selections
            .GroupBy(item => SourceKey(item.SourceType, item.SourceId), StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        if (hasDuplicate)
        {
            errors.Add("Cada origem pode aparecer apenas uma vez na reunião; escolha uma única seção.");
        }

        return errors.ToArray();
    }

    private static DailyMeetingOperationResult? ValidateMutable(DailyMeeting meeting, long version)
    {
        if (meeting.Status == MeetingStatus.Finished)
        {
            return DailyMeetingOperationResult.Finished(meeting.Id);
        }

        return meeting.Version == version ? null : DailyMeetingOperationResult.Conflict(meeting.Id);
    }

    private async Task<IReadOnlyList<MeetingSourceCandidate>> GetCandidatesAsync(
        CancellationToken cancellationToken)
    {
        DateTime completedSinceUtc = GetUtcNow().AddDays(-7);
        return await repository.GetSourceCandidatesAsync(completedSinceUtc, cancellationToken);
    }

    private static DailyMeetingBuilderRow CreateCandidateRow(MeetingSourceCandidate candidate, DateOnly today)
    {
        MeetingSection section = RecommendSection(candidate, today);
        bool prioritySuggestion = section is MeetingSection.Overdue
            or MeetingSection.DueSoon
            or MeetingSection.RecentlyCompleted;
        return new DailyMeetingBuilderRow(
            ItemId: null,
            candidate.SourceType,
            candidate.SourceId,
            SourceLabel(candidate.SourceType),
            candidate.Title,
            candidate.Status,
            candidate.DueDate,
            candidate.Responsible,
            Selected: prioritySuggestion,
            RecommendedSection: section,
            Section: section,
            SortOrder: candidate.NaturalOrder,
            PresentationNotes: null,
            SuggestionReason: SuggestionReason(section));
    }

    private static MeetingSection RecommendSection(MeetingSourceCandidate candidate, DateOnly today)
    {
        if (WorkItemDeadlineRules.IsOverdue(candidate.DueDate, candidate.LifecycleState, archivedAtUtc: null, today))
        {
            return MeetingSection.Overdue;
        }

        if (WorkItemDeadlineRules.IsDueSoon(candidate.DueDate, candidate.LifecycleState, archivedAtUtc: null, today))
        {
            return MeetingSection.DueSoon;
        }

        if (candidate.LifecycleState == LifecycleState.Completed)
        {
            return MeetingSection.RecentlyCompleted;
        }

        return candidate.SourceType switch
        {
            TrackedEntityType.PendingTask => MeetingSection.PendingTasks,
            TrackedEntityType.Smud => MeetingSection.Smuds,
            TrackedEntityType.SupportTicket => MeetingSection.SupportTickets,
            _ => throw new InvalidOperationException("Tipo de origem inválido para sugestão."),
        };
    }

    private static string SuggestionReason(MeetingSection section) => section switch
    {
        MeetingSection.Overdue => "Prazo vencido",
        MeetingSection.DueSoon => "Vence nos próximos 7 dias",
        MeetingSection.RecentlyCompleted => "Concluído nos últimos 7 dias",
        MeetingSection.PendingTasks => "Pendência ativa",
        MeetingSection.Smuds => "SMUD ativo",
        MeetingSection.SupportTickets => "Chamado ativo",
        _ => "Item operacional",
    };

    private static int SectionRank(MeetingSection section) => section switch
    {
        MeetingSection.Overdue => 0,
        MeetingSection.DueSoon => 1,
        MeetingSection.PendingTasks => 2,
        MeetingSection.Smuds => 3,
        MeetingSection.SupportTickets => 4,
        MeetingSection.RecentlyCompleted => 5,
        _ => 6,
    };

    private static DailyMeetingSummary MapSummary(DailyMeeting meeting) => new(
        meeting.Id,
        meeting.MeetingDate,
        meeting.Status,
        meeting.StartedAtUtc,
        meeting.FinishedAtUtc,
        meeting.Items.Count,
        meeting.Items.Count(item => item.WasPresented),
        meeting.Version);

    private static DailyMeetingDetailsData MapDetails(DailyMeeting meeting) => new(
        meeting.Id,
        meeting.MeetingDate,
        meeting.Status,
        meeting.StartedAtUtc,
        meeting.FinishedAtUtc,
        meeting.GeneralNotes,
        meeting.CreatedAtUtc,
        meeting.UpdatedAtUtc,
        meeting.Version,
        meeting.Items
            .OrderBy(item => SectionRank(item.Section))
            .ThenBy(item => item.SortOrder)
            .Select(MapItem)
            .ToList());

    private static DailyMeetingItemData MapItem(DailyMeetingItem item)
    {
        (TrackedEntityType sourceType, Guid sourceId) = GetSource(item);
        StatusDefinition? currentStatus = item.PendingTask?.StatusDefinition
            ?? item.Smud?.StatusDefinition
            ?? item.SupportTicket?.StatusDefinition;
        return new DailyMeetingItemData(
            item.Id,
            sourceType,
            sourceId,
            item.Section,
            item.SortOrder,
            item.PresentationNotes,
            item.WasPresented,
            item.SnapshotTitle,
            item.SnapshotStatus,
            item.SnapshotDueDate,
            item.SnapshotResponsible,
            currentStatus?.Name,
            currentStatus?.LifecycleState == LifecycleState.Completed);
    }

    private static (TrackedEntityType SourceType, Guid SourceId) GetSource(DailyMeetingItem item)
    {
        if (item.PendingTaskId.HasValue)
        {
            return (TrackedEntityType.PendingTask, item.PendingTaskId.Value);
        }

        if (item.SmudId.HasValue)
        {
            return (TrackedEntityType.Smud, item.SmudId.Value);
        }

        if (item.SupportTicketId.HasValue)
        {
            return (TrackedEntityType.SupportTicket, item.SupportTicketId.Value);
        }

        throw new InvalidOperationException("O item da reunião não possui origem.");
    }

    private void AddMeetingHistory(
        Guid meetingId,
        Guid actorUserId,
        ActivityActionType action,
        string summary) =>
        repository.AddHistory(new ActivityHistory
        {
            Id = Guid.NewGuid(),
            EntityType = TrackedEntityType.DailyMeeting,
            EntityId = meetingId,
            ActionType = action,
            OccurredAtUtc = GetUtcNow(),
            ActorUserId = actorUserId,
            Summary = summary,
        });

    private void AddSourceHistory(
        TrackedEntityType sourceType,
        Guid sourceId,
        Guid actorUserId,
        string summary) =>
        repository.AddHistory(new ActivityHistory
        {
            Id = Guid.NewGuid(),
            EntityType = sourceType,
            EntityId = sourceId,
            ActionType = ActivityActionType.Completed,
            OccurredAtUtc = GetUtcNow(),
            ActorUserId = actorUserId,
            Summary = summary,
        });

    private static bool IsMeetingSource(TrackedEntityType type) => type is
        TrackedEntityType.PendingTask or TrackedEntityType.Smud or TrackedEntityType.SupportTicket;

    private static string SourceKey(TrackedEntityType sourceType, Guid sourceId) => $"{sourceType}:{sourceId:N}";

    private static string SourceLabel(TrackedEntityType sourceType) => sourceType switch
    {
        TrackedEntityType.PendingTask => "Pendência",
        TrackedEntityType.Smud => "SMUD",
        TrackedEntityType.SupportTicket => "Chamado",
        _ => "Item",
    };

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private DateOnly GetToday() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
