using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;

namespace CentraSA.Application.Smuds;

public enum SmudDueFilter
{
    All,
    Overdue,
    DueSoon,
    NoDueDate,
}

public enum SmudOperationStatus
{
    Success,
    NotFound,
    ValidationFailed,
    DuplicateCode,
    Conflict,
}

public sealed record SmudSearch(
    string? Search,
    Guid? AreaId,
    Guid? PersonId,
    Guid? StatusId,
    SmudDueFilter DueFilter,
    bool ActionRequiredOnly,
    bool HideFinalized,
    bool ArchivedOnly,
    DateOnly Today);

public sealed record SmudReferenceData(
    IReadOnlyList<TeamArea> Areas,
    IReadOnlyList<Person> People,
    IReadOnlyList<StatusDefinition> Statuses);

public sealed record SmudLookupOption(
    Guid Id,
    string Name,
    string? ColorToken = null,
    string? Group = null);

public sealed record SmudFilterOptions(
    IReadOnlyList<SmudLookupOption> Areas,
    IReadOnlyList<SmudLookupOption> People,
    IReadOnlyList<SmudLookupOption> Statuses);

public sealed record SmudFormOptions(
    IReadOnlyList<SmudLookupOption> Areas,
    IReadOnlyList<SmudLookupOption> People,
    IReadOnlyList<SmudLookupOption> Statuses);

public sealed record SmudBoardCard(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    string Area,
    string? Person,
    string Status,
    string StatusColor,
    LifecycleState LifecycleState,
    PriorityLevel Priority,
    DateOnly? OpenedOn,
    DateOnly? DueDate,
    string? RequiredAction,
    bool IsOverdue,
    bool IsDueSoon,
    bool RequiresAction,
    long Version);

public sealed record SmudBoardColumn(
    Guid StatusId,
    string Status,
    string ColorToken,
    LifecycleState LifecycleState,
    int SortOrder,
    IReadOnlyList<SmudBoardCard> Cards)
{
    public int Total => Cards.Count;
}

public sealed record SmudBoardData(
    IReadOnlyList<SmudBoardColumn> Columns,
    SmudFilterOptions Options,
    bool ArchivedOnly)
{
    public int TotalCount => Columns.Sum(column => column.Total);
}

public sealed record SmudHistoryItem(
    string Action,
    string Summary,
    DateTime OccurredAtUtc);

public sealed record SmudDetailsData(
    SmudBoardCard Item,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<SmudHistoryItem> History);

public sealed class SmudInput
{
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid ResponsibleAreaId { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    public Guid StatusId { get; set; }

    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public DateOnly? OpenedOn { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? RequiredAction { get; set; }

    public string? Notes { get; set; }

    public long Version { get; set; }
}

public sealed record SmudEditorData(
    Guid? Id,
    SmudInput Input,
    SmudFormOptions Options);

public sealed record SmudOperationResult(
    SmudOperationStatus Status,
    Guid? Id = null,
    IReadOnlyList<string>? Errors = null)
{
    public bool Succeeded => Status == SmudOperationStatus.Success;

    public static SmudOperationResult Success(Guid id) => new(SmudOperationStatus.Success, id);

    public static SmudOperationResult NotFound() => new(SmudOperationStatus.NotFound);

    public static SmudOperationResult Conflict(Guid id) => new(
        SmudOperationStatus.Conflict,
        id,
        ["O SMUD foi alterado em outra aba. Recarregue a página e tente novamente."]);

    public static SmudOperationResult DuplicateCode(Guid? id = null) => new(
        SmudOperationStatus.DuplicateCode,
        id,
        ["Já existe um SMUD com esse código."]);

    public static SmudOperationResult Invalid(params string[] errors) => new(
        SmudOperationStatus.ValidationFailed,
        Errors: errors);
}
