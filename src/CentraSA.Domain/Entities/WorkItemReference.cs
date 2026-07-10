namespace CentraSA.Domain.Entities;

public sealed class WorkItemReference
{
    public Guid Id { get; set; }

    public Guid PendingTaskId { get; set; }

    public PendingTask PendingTask { get; set; } = null!;

    public Guid? SmudId { get; set; }

    public Smud? Smud { get; set; }

    public Guid? SupportTicketId { get; set; }

    public SupportTicket? SupportTicket { get; set; }
}
