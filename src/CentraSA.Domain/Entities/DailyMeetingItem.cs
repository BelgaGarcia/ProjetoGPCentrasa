using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Entities;

public sealed class DailyMeetingItem
{
    public Guid Id { get; set; }

    public Guid DailyMeetingId { get; set; }

    public DailyMeeting DailyMeeting { get; set; } = null!;

    public Guid? PendingTaskId { get; set; }

    public PendingTask? PendingTask { get; set; }

    public Guid? SmudId { get; set; }

    public Smud? Smud { get; set; }

    public Guid? SupportTicketId { get; set; }

    public SupportTicket? SupportTicket { get; set; }

    public MeetingSection Section { get; set; }

    public int SortOrder { get; set; }

    public string? PresentationNotes { get; set; }

    public bool WasPresented { get; set; }

    public string SnapshotTitle { get; set; } = string.Empty;

    public string SnapshotStatus { get; set; } = string.Empty;

    public DateOnly? SnapshotDueDate { get; set; }

    public string? SnapshotResponsible { get; set; }
}
