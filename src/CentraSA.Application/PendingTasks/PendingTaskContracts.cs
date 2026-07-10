using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;

namespace CentraSA.Application.PendingTasks;

public enum PendingTaskDueFilter
{
    All,
    Overdue,
    DueSoon,
    NoDueDate,
}

public enum PendingTaskMoveDirection
{
    Up,
    Down,
}

public enum PendingTaskOperationStatus
{
    Success,
    NotFound,
    ValidationFailed,
    Conflict,
}

public sealed record PendingTaskSearch(
    string? Search,
    Guid? AreaId,
    Guid? PersonId,
    Guid? StatusId,
    PendingTaskDueFilter DueFilter,
    bool HideCompleted,
    bool ArchivedOnly,
    DateOnly Today,
    int Page = 1,
    int PageSize = 50);

public sealed record PendingTaskPage(
    IReadOnlyList<PendingTask> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record PendingTaskReferenceData(
    IReadOnlyList<TeamArea> Areas,
    IReadOnlyList<Person> People,
    IReadOnlyList<StatusDefinition> Statuses,
    IReadOnlyList<Category> Categories,
    IReadOnlyList<Smud> Smuds,
    IReadOnlyList<SupportTicket> SupportTickets);

public sealed record LookupOption(
    Guid Id,
    string Name,
    string? ColorToken = null,
    string? Group = null);

public sealed record PendingTaskFilterOptions(
    IReadOnlyList<LookupOption> Areas,
    IReadOnlyList<LookupOption> People,
    IReadOnlyList<LookupOption> Statuses);

public sealed record PendingTaskFormOptions(
    IReadOnlyList<LookupOption> Areas,
    IReadOnlyList<LookupOption> People,
    IReadOnlyList<LookupOption> Statuses,
    IReadOnlyList<LookupOption> Categories,
    IReadOnlyList<LookupOption> Smuds,
    IReadOnlyList<LookupOption> SupportTickets);

public sealed record PendingTaskListItem(
    Guid Id,
    string Title,
    string? Description,
    string Area,
    string? Person,
    string? Category,
    string Status,
    string StatusColor,
    PriorityLevel Priority,
    DateOnly? DueDate,
    DateTime? CompletedAtUtc,
    bool IsOverdue,
    bool IsDueSoon,
    int PresentationOrder,
    long Version,
    string? RelatedItem);

public sealed record PendingTaskListData(
    IReadOnlyList<PendingTaskListItem> Items,
    PendingTaskFilterOptions Options,
    int TotalCount,
    int Page,
    int PageSize,
    bool ArchivedOnly)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record ActivityHistoryItem(
    string Action,
    string Summary,
    DateTime OccurredAtUtc);

public sealed record PendingTaskDetailsData(
    PendingTaskListItem Item,
    string? Origin,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<ActivityHistoryItem> History);

public sealed class PendingTaskInput
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid ResponsibleAreaId { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    public Guid StatusId { get; set; }

    public Guid? CategoryId { get; set; }

    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public DateOnly? DueDate { get; set; }

    public string? Origin { get; set; }

    public string? Notes { get; set; }

    public Guid? RelatedSmudId { get; set; }

    public Guid? RelatedSupportTicketId { get; set; }

    public long Version { get; set; }
}

public sealed record PendingTaskQuickInput(
    string Title,
    Guid ResponsibleAreaId,
    DateOnly? DueDate);

public sealed record PendingTaskEditorData(
    Guid? Id,
    PendingTaskInput Input,
    PendingTaskFormOptions Options);

public sealed record PendingTaskOperationResult(
    PendingTaskOperationStatus Status,
    Guid? Id = null,
    IReadOnlyList<string>? Errors = null)
{
    public bool Succeeded => Status == PendingTaskOperationStatus.Success;

    public static PendingTaskOperationResult Success(Guid id) => new(PendingTaskOperationStatus.Success, id);

    public static PendingTaskOperationResult NotFound() => new(PendingTaskOperationStatus.NotFound);

    public static PendingTaskOperationResult Conflict(Guid id) => new(
        PendingTaskOperationStatus.Conflict,
        id,
        ["O registro foi alterado em outra aba. Recarregue a página e tente novamente."]);

    public static PendingTaskOperationResult Invalid(params string[] errors) => new(
        PendingTaskOperationStatus.ValidationFailed,
        Errors: errors);
}
