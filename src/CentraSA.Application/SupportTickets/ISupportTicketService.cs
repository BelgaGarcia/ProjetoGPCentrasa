namespace CentraSA.Application.SupportTickets;

public interface ISupportTicketService
{
    Task<SupportTicketBoardData> SearchAsync(
        SupportTicketSearch search,
        CancellationToken cancellationToken = default);

    Task<SupportTicketBoardData> GetPresentationAsync(
        bool showFinalized,
        CancellationToken cancellationToken = default);

    Task<SupportTicketEditorData> GetCreateEditorAsync(CancellationToken cancellationToken = default);

    Task<SupportTicketEditorData?> GetEditEditorAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SupportTicketDetailsData?> GetDetailsAsync(
        Guid id,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<SupportTicketOperationResult> CreateAsync(
        SupportTicketInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketOperationResult> UpdateAsync(
        Guid id,
        SupportTicketInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketOperationResult> ArchiveAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<SupportTicketOperationResult> RestoreAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
