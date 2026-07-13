namespace CentraSA.Application.Smuds;

public interface ISmudService
{
    Task<SmudBoardData> SearchAsync(SmudSearch search, CancellationToken cancellationToken = default);

    Task<SmudBoardData> GetPresentationAsync(
        bool showFinalized,
        CancellationToken cancellationToken = default);

    Task<SmudEditorData> GetCreateEditorAsync(CancellationToken cancellationToken = default);

    Task<SmudEditorData?> GetEditEditorAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SmudDetailsData?> GetDetailsAsync(
        Guid id,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<SmudOperationResult> CreateAsync(
        SmudInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<SmudOperationResult> UpdateAsync(
        Guid id,
        SmudInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<SmudOperationResult> ArchiveAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<SmudOperationResult> RestoreAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
