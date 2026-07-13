using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;

namespace CentraSA.Application.Insights;

public sealed class InsightService(
    IInsightRepository repository,
    TimeProvider timeProvider) : IInsightService
{
    public async Task<DashboardData> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = GetUtcNow();
        DateTime recentSince = now.AddDays(-7);
        DateOnly today = GetToday();
        IReadOnlyList<OperationalItemProjection> items = await repository.GetOperationalItemsAsync(
            recentSince,
            cancellationToken);
        List<DashboardItemData> mapped = items.Select(item => MapItem(item, today)).ToList();
        HistoryRepositoryPage history = await repository.SearchHistoryAsync(
            new HistoryRepositorySearch(null, null, null, null, null, Page: 1, PageSize: 8),
            cancellationToken);

        return new DashboardData(
            ActivePendingTasks: CountActive(items, TrackedEntityType.PendingTask),
            ActiveSmuds: CountActive(items, TrackedEntityType.Smud),
            ActiveSupportTickets: CountActive(items, TrackedEntityType.SupportTicket),
            OverdueCount: mapped.Count(item => item.IsOverdue),
            DueSoonCount: mapped.Count(item => item.IsDueSoon),
            RecentlyCompletedCount: items.Count(item => item.LifecycleState == LifecycleState.Completed
                && item.CompletedAtUtc >= recentSince),
            DraftMeetingCount: await repository.CountDraftMeetingsAsync(cancellationToken),
            Deadlines: mapped
                .Where(item => item.LifecycleState == LifecycleState.Active && item.DueDate.HasValue)
                .OrderBy(item => item.DueDate)
                .ThenBy(item => item.SourceType)
                .ThenBy(item => item.Reference)
                .Take(8)
                .ToList(),
            RecentActivity: history.Items.Select(MapHistory).ToList());
    }

    public async Task<DashboardItemListData> SearchItemsAsync(
        string? search,
        TrackedEntityType? sourceType,
        DashboardItemStateFilter state,
        CancellationToken cancellationToken = default)
    {
        DateTime recentSince = GetUtcNow().AddDays(-7);
        DateOnly today = GetToday();
        IReadOnlyList<OperationalItemProjection> items = await repository.GetOperationalItemsAsync(
            recentSince,
            cancellationToken);
        IEnumerable<OperationalItemProjection> query = items;

        if (sourceType.HasValue)
        {
            query = query.Where(item => item.SourceType == sourceType);
        }

        string? term = NormalizeOptional(search);
        if (term is not null)
        {
            query = query.Where(item => item.Reference.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.Status.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        query = state switch
        {
            DashboardItemStateFilter.Active => query.Where(item => item.LifecycleState == LifecycleState.Active),
            DashboardItemStateFilter.Overdue => query.Where(item => IsOverdue(item, today)),
            DashboardItemStateFilter.DueSoon => query.Where(item => IsDueSoon(item, today)),
            DashboardItemStateFilter.RecentlyCompleted => query.Where(item =>
                item.LifecycleState == LifecycleState.Completed && item.CompletedAtUtc >= recentSince),
            _ => query,
        };

        var normalizedSearch = new DashboardItemSearch(term, sourceType, state, today, recentSince);
        List<DashboardItemData> result = query
            .Select(item => MapItem(item, today))
            .OrderByDescending(item => item.IsOverdue)
            .ThenBy(item => item.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Reference)
            .ToList();
        return new DashboardItemListData(result, normalizedSearch);
    }

    public async Task<GlobalHistoryPage> SearchHistoryAsync(
        GlobalHistorySearch search,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, search.Page);
        int pageSize = Math.Clamp(search.PageSize, 1, 100);
        string? term = NormalizeOptional(search.Search);
        DateTime? fromUtc = search.FromDate.HasValue ? ToUtc(search.FromDate.Value) : null;
        DateTime? toExclusiveUtc = search.ToDate.HasValue ? ToUtc(search.ToDate.Value.AddDays(1)) : null;
        var normalized = search with { Search = term, Page = page, PageSize = pageSize };
        HistoryRepositoryPage result = await repository.SearchHistoryAsync(
            new HistoryRepositorySearch(
                term,
                search.EntityType,
                search.ActionType,
                fromUtc,
                toExclusiveUtc,
                page,
                pageSize),
            cancellationToken);
        return new GlobalHistoryPage(
            result.Items.Select(MapHistory).ToList(),
            result.TotalCount,
            page,
            pageSize,
            normalized);
    }

    public async Task<HistoryDetailsData?> GetHistoryDetailsAsync(
        TrackedEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        HistorySubject? subject = await repository.GetHistorySubjectAsync(entityType, entityId, cancellationToken);
        if (subject is null)
        {
            return null;
        }

        IReadOnlyList<HistoryRepositoryEntry> events = await repository.GetEntityHistoryAsync(
            entityType,
            entityId,
            cancellationToken);
        return new HistoryDetailsData(subject, events.Select(MapHistory).ToList());
    }

    private static int CountActive(
        IEnumerable<OperationalItemProjection> items,
        TrackedEntityType sourceType) =>
        items.Count(item => item.SourceType == sourceType && item.LifecycleState == LifecycleState.Active);

    private static DashboardItemData MapItem(OperationalItemProjection item, DateOnly today) => new(
        item.SourceType,
        item.SourceId,
        EntityLabel(item.SourceType),
        item.Reference,
        item.Title,
        item.Status,
        item.LifecycleState,
        item.DueDate,
        item.UpdatedAtUtc,
        IsOverdue(item, today),
        IsDueSoon(item, today));

    private static bool IsOverdue(OperationalItemProjection item, DateOnly today) =>
        WorkItemDeadlineRules.IsOverdue(item.DueDate, item.LifecycleState, archivedAtUtc: null, today);

    private static bool IsDueSoon(OperationalItemProjection item, DateOnly today) =>
        WorkItemDeadlineRules.IsDueSoon(item.DueDate, item.LifecycleState, archivedAtUtc: null, today);

    private static GlobalHistoryEntry MapHistory(HistoryRepositoryEntry entry) => new(
        entry.Id,
        entry.EntityType,
        entry.EntityId,
        EntityLabel(entry.EntityType),
        entry.ActionType,
        ActionLabel(entry.ActionType),
        entry.Summary,
        entry.OccurredAtUtc);

    private static string EntityLabel(TrackedEntityType type) => type switch
    {
        TrackedEntityType.PendingTask => "Pendência",
        TrackedEntityType.Smud => "SMUD",
        TrackedEntityType.SupportTicket => "Chamado",
        TrackedEntityType.DailyMeeting => "Reunião",
        TrackedEntityType.TeamArea => "Área/equipe",
        TrackedEntityType.Person => "Pessoa",
        TrackedEntityType.StatusDefinition => "Status",
        TrackedEntityType.Category => "Categoria",
        TrackedEntityType.Backup => "Backup",
        _ => "Registro",
    };

    private static string ActionLabel(ActivityActionType action) => action switch
    {
        ActivityActionType.Created => "Criação",
        ActivityActionType.Updated => "Atualização",
        ActivityActionType.StatusChanged => "Mudança de status",
        ActivityActionType.ResponsibleChanged => "Mudança de responsável",
        ActivityActionType.DueDateChanged => "Mudança de prazo",
        ActivityActionType.Completed => "Conclusão",
        ActivityActionType.Reopened => "Reabertura",
        ActivityActionType.Archived => "Desativação/arquivamento",
        ActivityActionType.Restored => "Ativação/restauração",
        ActivityActionType.BackupRestored => "Restauração de backup",
        _ => action.ToString(),
    };

    private DateTime ToUtc(DateOnly date)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeProvider.LocalTimeZone);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private DateOnly GetToday() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
