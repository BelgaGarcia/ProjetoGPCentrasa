using CentraSA.Domain.Common;
using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Entities;

public sealed class DailyMeeting : IConcurrencyTracked
{
    public Guid Id { get; set; }

    public DateOnly MeetingDate { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public MeetingStatus Status { get; set; } = MeetingStatus.Draft;

    public string? GeneralNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }

    public long Version { get; set; } = 1;

    public ICollection<DailyMeetingItem> Items { get; } = new List<DailyMeetingItem>();
}
