namespace CentraSA.Application.Insights;

public interface IInsightRepository
{
    Task<IReadOnlyList<OperationalItemProjection>> GetOperationalItemsAsync(
        DateTime completedSinceUtc,
        CancellationToken cancellationToken = default);

    Task<int> CountDraftMeetingsAsync(CancellationToken cancellationToken = default);

    Task<HistoryRepositoryPage> SearchHistoryAsync(
        HistoryRepositorySearch search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryRepositoryEntry>> GetEntityHistoryAsync(
        CentraSA.Domain.Enums.TrackedEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    Task<HistorySubject?> GetHistorySubjectAsync(
        CentraSA.Domain.Enums.TrackedEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);
}
