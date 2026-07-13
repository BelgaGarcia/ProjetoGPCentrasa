using CentraSA.Domain.Enums;

namespace CentraSA.Application.Insights;

public enum DashboardItemStateFilter
{
    All,
    Active,
    Overdue,
    DueSoon,
    RecentlyCompleted,
}

public sealed record OperationalItemProjection(
    TrackedEntityType SourceType,
    Guid SourceId,
    string Reference,
    string Title,
    string Status,
    LifecycleState LifecycleState,
    DateOnly? DueDate,
    DateTime? CompletedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DashboardItemSearch(
    string? Search,
    TrackedEntityType? SourceType,
    DashboardItemStateFilter State,
    DateOnly Today,
    DateTime RecentSinceUtc);

public sealed record DashboardItemData(
    TrackedEntityType SourceType,
    Guid SourceId,
    string SourceLabel,
    string Reference,
    string Title,
    string Status,
    LifecycleState LifecycleState,
    DateOnly? DueDate,
    DateTime UpdatedAtUtc,
    bool IsOverdue,
    bool IsDueSoon);

public sealed record DashboardItemListData(
    IReadOnlyList<DashboardItemData> Items,
    DashboardItemSearch Search);

public sealed record DashboardData(
    int ActivePendingTasks,
    int ActiveSmuds,
    int ActiveSupportTickets,
    int OverdueCount,
    int DueSoonCount,
    int RecentlyCompletedCount,
    int DraftMeetingCount,
    IReadOnlyList<DashboardItemData> Deadlines,
    IReadOnlyList<GlobalHistoryEntry> RecentActivity);

public sealed record GlobalHistorySearch(
    string? Search,
    TrackedEntityType? EntityType,
    ActivityActionType? ActionType,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int PageSize = 50);

public sealed record HistoryRepositorySearch(
    string? Search,
    TrackedEntityType? EntityType,
    ActivityActionType? ActionType,
    DateTime? FromUtc,
    DateTime? ToExclusiveUtc,
    int Page,
    int PageSize);

public sealed record GlobalHistoryEntry(
    Guid Id,
    TrackedEntityType EntityType,
    Guid EntityId,
    string EntityLabel,
    ActivityActionType ActionType,
    string ActionLabel,
    string Summary,
    DateTime OccurredAtUtc);

public sealed record GlobalHistoryPage(
    IReadOnlyList<GlobalHistoryEntry> Items,
    int TotalCount,
    int Page,
    int PageSize,
    GlobalHistorySearch Search)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record HistorySubject(
    TrackedEntityType EntityType,
    Guid EntityId,
    string Title,
    string? Subtitle);

public sealed record HistoryDetailsData(
    HistorySubject Subject,
    IReadOnlyList<GlobalHistoryEntry> Events);

public sealed record HistoryRepositoryPage(
    IReadOnlyList<HistoryRepositoryEntry> Items,
    int TotalCount);

public sealed record HistoryRepositoryEntry(
    Guid Id,
    TrackedEntityType EntityType,
    Guid EntityId,
    ActivityActionType ActionType,
    string Summary,
    DateTime OccurredAtUtc);
