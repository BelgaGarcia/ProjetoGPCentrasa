namespace CentraSA.Application.Lookups;

public interface ILookupService
{
    Task<LookupOverviewData> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<LookupEditorData> GetCreateEditorAsync(
        LookupKind kind,
        CancellationToken cancellationToken = default);

    Task<LookupEditorData?> GetEditEditorAsync(
        LookupKind kind,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<LookupOperationResult> CreateAsync(
        LookupInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<LookupOperationResult> UpdateAsync(
        LookupKind kind,
        Guid id,
        LookupInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<LookupOperationResult> ToggleActiveAsync(
        LookupKind kind,
        Guid id,
        bool activate,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
