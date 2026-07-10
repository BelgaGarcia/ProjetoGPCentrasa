using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;

namespace CentraSA.Application.PendingTasks;

public interface IPendingTaskRepository
{
    Task<PendingTaskPage> SearchAsync(PendingTaskSearch search, CancellationToken cancellationToken = default);

    Task<PendingTask?> GetByIdAsync(
        Guid id,
        bool includeArchived,
        bool track,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingTask>> GetOrderedActiveAsync(CancellationToken cancellationToken = default);

    Task<PendingTaskReferenceData> GetReferenceDataAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityHistory>> GetHistoryAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task<int> GetNextPresentationOrderAsync(CancellationToken cancellationToken = default);

    void Add(PendingTask task);

    void AddHistory(ActivityHistory history);

    Task ReplaceReferenceAsync(
        Guid taskId,
        Guid? smudId,
        Guid? supportTicketId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
