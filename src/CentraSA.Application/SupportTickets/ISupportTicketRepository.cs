using CentraSA.Domain.Entities;

namespace CentraSA.Application.SupportTickets;

public interface ISupportTicketRepository
{
    Task<IReadOnlyList<SupportTicket>> SearchAsync(
        SupportTicketSearch search,
        CancellationToken cancellationToken = default);

    Task<SupportTicket?> GetByIdAsync(
        Guid id,
        bool includeArchived,
        bool track,
        CancellationToken cancellationToken = default);

    Task<SupportTicketReferenceData> GetReferenceDataAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityHistory>> GetHistoryAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<bool> IsNumberInUseAsync(
        string normalizedNumber,
        Guid? excludingId,
        CancellationToken cancellationToken = default);

    void Add(SupportTicket ticket);

    void AddHistory(ActivityHistory history);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
