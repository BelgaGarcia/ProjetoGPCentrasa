using CentraSA.Domain.Enums;

namespace CentraSA.Application.DailyMeetings;

public enum DailyMeetingOperationStatus
{
    Success,
    NotFound,
    ValidationFailed,
    Conflict,
    AlreadyFinished,
}

public sealed record MeetingSourceCandidate(
    TrackedEntityType SourceType,
    Guid SourceId,
    string Title,
    string Status,
    LifecycleState LifecycleState,
    DateOnly? DueDate,
    string? Responsible,
    DateTime? CompletedAtUtc,
    int NaturalOrder);

public sealed class DailyMeetingSelectionInput
{
    public Guid? ItemId { get; set; }

    public TrackedEntityType SourceType { get; set; }

    public Guid SourceId { get; set; }

    public bool Selected { get; set; }

    public MeetingSection Section { get; set; }

    public int SortOrder { get; set; }

    public string? PresentationNotes { get; set; }
}

public sealed class DailyMeetingInput
{
    public DateOnly MeetingDate { get; set; }

    public string? GeneralNotes { get; set; }

    public long Version { get; set; }

    public List<DailyMeetingSelectionInput> Items { get; set; } = [];
}

public sealed record DailyMeetingBuilderRow(
    Guid? ItemId,
    TrackedEntityType SourceType,
    Guid SourceId,
    string SourceLabel,
    string Title,
    string Status,
    DateOnly? DueDate,
    string? Responsible,
    bool Selected,
    MeetingSection RecommendedSection,
    MeetingSection Section,
    int SortOrder,
    string? PresentationNotes,
    string SuggestionReason);

public sealed record DailyMeetingBuilderData(
    Guid? Id,
    DateOnly MeetingDate,
    string? GeneralNotes,
    long Version,
    IReadOnlyList<DailyMeetingBuilderRow> Rows);

public sealed record DailyMeetingSummary(
    Guid Id,
    DateOnly MeetingDate,
    MeetingStatus Status,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    int ItemCount,
    int PresentedCount,
    long Version);

public sealed record DailyMeetingOverviewData(
    IReadOnlyList<DailyMeetingSummary> Meetings,
    DailyMeetingSummary? LatestMeeting);

public sealed record DailyMeetingItemData(
    Guid Id,
    TrackedEntityType SourceType,
    Guid SourceId,
    MeetingSection Section,
    int SortOrder,
    string? PresentationNotes,
    bool WasPresented,
    string SnapshotTitle,
    string SnapshotStatus,
    DateOnly? SnapshotDueDate,
    string? SnapshotResponsible,
    string? CurrentStatus,
    bool OriginalIsCompleted);

public sealed record DailyMeetingDetailsData(
    Guid Id,
    DateOnly MeetingDate,
    MeetingStatus Status,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    string? GeneralNotes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    long Version,
    IReadOnlyList<DailyMeetingItemData> Items)
{
    public int PresentedCount => Items.Count(item => item.WasPresented);
}

public sealed record DailyMeetingOperationResult(
    DailyMeetingOperationStatus Status,
    Guid? Id = null,
    IReadOnlyList<string>? Errors = null)
{
    public bool Succeeded => Status == DailyMeetingOperationStatus.Success;

    public static DailyMeetingOperationResult Success(Guid id) => new(DailyMeetingOperationStatus.Success, id);

    public static DailyMeetingOperationResult NotFound() => new(DailyMeetingOperationStatus.NotFound);

    public static DailyMeetingOperationResult Finished(Guid id) => new(
        DailyMeetingOperationStatus.AlreadyFinished,
        id,
        ["A reunião já foi finalizada e permanece disponível somente para consulta."]);

    public static DailyMeetingOperationResult Conflict(Guid id) => new(
        DailyMeetingOperationStatus.Conflict,
        id,
        ["A reunião foi alterada em outra aba. Recarregue a página e tente novamente."]);

    public static DailyMeetingOperationResult Invalid(params string[] errors) => new(
        DailyMeetingOperationStatus.ValidationFailed,
        Errors: errors);
}
