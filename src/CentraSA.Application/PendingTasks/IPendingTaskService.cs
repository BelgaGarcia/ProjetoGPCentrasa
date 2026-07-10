namespace CentraSA.Application.PendingTasks;

public interface IPendingTaskService
{
    Task<PendingTaskListData> SearchAsync(PendingTaskSearch search, CancellationToken cancellationToken = default);

    Task<PendingTaskListData> GetPresentationAsync(bool showCompleted, CancellationToken cancellationToken = default);

    Task<PendingTaskEditorData> GetCreateEditorAsync(CancellationToken cancellationToken = default);

    Task<PendingTaskEditorData?> GetEditEditorAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PendingTaskDetailsData?> GetDetailsAsync(Guid id, bool includeArchived, CancellationToken cancellationToken = default);

    Task<PendingTaskOperationResult> CreateAsync(PendingTaskInput input, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<PendingTaskOperationResult> QuickCreateAsync(PendingTaskQuickInput input, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<PendingTaskOperationResult> UpdateAsync(Guid id, PendingTaskInput input, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<PendingTaskOperationResult> CompleteAsync(Guid id, long version, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<PendingTaskOperationResult> ReopenAsync(Guid id, long version, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<PendingTaskOperationResult> ArchiveAsync(Guid id, long version, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<PendingTaskOperationResult> RestoreAsync(Guid id, long version, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<PendingTaskOperationResult> MoveAsync(Guid id, long version, PendingTaskMoveDirection direction, Guid actorUserId, CancellationToken cancellationToken = default);
}
