using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;

namespace CentraSA.Application.SupportTickets;

public enum SupportTicketDueFilter
{
    All,
    Overdue,
    DueSoon,
    NoDueDate,
}

public enum SupportTicketOperationStatus
{
    Success,
    NotFound,
    ValidationFailed,
    DuplicateNumber,
    Conflict,
}

public sealed record SupportTicketSearch(
    string? Search,
    Guid? CategoryId,
    Guid? AreaId,
    Guid? PersonId,
    Guid? StatusId,
    SupportTicketDueFilter DueFilter,
    bool ActionRequiredOnly,
    bool HideFinalized,
    bool ArchivedOnly,
    DateOnly Today);

public sealed record SupportTicketReferenceData(
    IReadOnlyList<TeamArea> Areas,
    IReadOnlyList<Person> People,
    IReadOnlyList<StatusDefinition> Statuses,
    IReadOnlyList<Category> Categories);

public sealed record SupportTicketLookupOption(
    Guid Id,
    string Name,
    string? ColorToken = null,
    string? Group = null);

public sealed record SupportTicketFilterOptions(
    IReadOnlyList<SupportTicketLookupOption> Categories,
    IReadOnlyList<SupportTicketLookupOption> Areas,
    IReadOnlyList<SupportTicketLookupOption> People,
    IReadOnlyList<SupportTicketLookupOption> Statuses);

public sealed record SupportTicketFormOptions(
    IReadOnlyList<SupportTicketLookupOption> Categories,
    IReadOnlyList<SupportTicketLookupOption> Areas,
    IReadOnlyList<SupportTicketLookupOption> People,
    IReadOnlyList<SupportTicketLookupOption> Statuses);

public sealed record SupportTicketBoardCard(
    Guid Id,
    string Number,
    string Title,
    string? Description,
    string Category,
    string CategoryColor,
    string Area,
    string AreaColor,
    string? Person,
    string Status,
    string StatusColor,
    LifecycleState LifecycleState,
    PriorityLevel Priority,
    DateOnly OpenedOn,
    DateOnly? DueDate,
    string? PendingAction,
    bool IsOverdue,
    bool IsDueSoon,
    bool RequiresAction,
    long Version);

public sealed record SupportTicketBoardGroup(
    Guid CategoryId,
    string Category,
    string ColorToken,
    int SortOrder,
    IReadOnlyList<SupportTicketBoardCard> Cards)
{
    public int Total => Cards.Count;
}

public sealed record SupportTicketBoardData(
    IReadOnlyList<SupportTicketBoardGroup> Groups,
    SupportTicketFilterOptions Options,
    bool ArchivedOnly)
{
    public int TotalCount => Groups.Sum(group => group.Total);
}

public sealed record SupportTicketHistoryItem(
    string Action,
    string Summary,
    DateTime OccurredAtUtc);

public sealed record SupportTicketDetailsData(
    SupportTicketBoardCard Item,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<SupportTicketHistoryItem> History);

public sealed class SupportTicketInput
{
    public string Number { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid CategoryId { get; set; }

    public Guid ResponsibleAreaId { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    public Guid StatusId { get; set; }

    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public DateOnly OpenedOn { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? PendingAction { get; set; }

    public string? Notes { get; set; }

    public long Version { get; set; }
}

public sealed record SupportTicketEditorData(
    Guid? Id,
    SupportTicketInput Input,
    SupportTicketFormOptions Options);

public sealed record SupportTicketOperationResult(
    SupportTicketOperationStatus Status,
    Guid? Id = null,
    IReadOnlyList<string>? Errors = null)
{
    public bool Succeeded => Status == SupportTicketOperationStatus.Success;

    public static SupportTicketOperationResult Success(Guid id) => new(SupportTicketOperationStatus.Success, id);

    public static SupportTicketOperationResult NotFound() => new(SupportTicketOperationStatus.NotFound);

    public static SupportTicketOperationResult Conflict(Guid id) => new(
        SupportTicketOperationStatus.Conflict,
        id,
        ["O chamado foi alterado em outra aba. Recarregue a página e tente novamente."]);

    public static SupportTicketOperationResult DuplicateNumber(Guid? id = null) => new(
        SupportTicketOperationStatus.DuplicateNumber,
        id,
        ["Já existe um chamado com esse número."]);

    public static SupportTicketOperationResult Invalid(params string[] errors) => new(
        SupportTicketOperationStatus.ValidationFailed,
        Errors: errors);
}
