using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;

namespace CentraSA.Application.Lookups;

public enum LookupKind
{
    TeamArea,
    Person,
    Status,
    Category,
}

public enum LookupOperationStatus
{
    Success,
    NotFound,
    ValidationFailed,
    Duplicate,
}

public sealed record LookupReferenceData(
    IReadOnlyList<TeamArea> Areas,
    IReadOnlyList<Person> People,
    IReadOnlyList<StatusDefinition> Statuses,
    IReadOnlyList<Category> Categories);

public sealed record LookupListItem(
    Guid Id,
    LookupKind Kind,
    string Name,
    string? Code,
    string Context,
    string ColorToken,
    int? SortOrder,
    bool IsActive);

public sealed record LookupAreaOption(Guid Id, string Name);

public sealed record LookupOverviewData(
    IReadOnlyList<LookupListItem> Areas,
    IReadOnlyList<LookupListItem> People,
    IReadOnlyList<LookupListItem> Statuses,
    IReadOnlyList<LookupListItem> Categories);

public sealed class LookupInput
{
    public Guid? Id { get; set; }

    public LookupKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public WorkItemScope Scope { get; set; }

    public TeamAreaKind AreaKind { get; set; }

    public Guid? TeamAreaId { get; set; }

    public LifecycleState LifecycleState { get; set; } = LifecycleState.Active;

    public string ColorToken { get; set; } = "blue";

    public int SortOrder { get; set; } = 10;
}

public sealed record LookupEditorData(
    LookupInput Input,
    IReadOnlyList<LookupAreaOption> Areas);

public sealed record LookupOperationResult(
    LookupOperationStatus Status,
    Guid? Id = null,
    IReadOnlyList<string>? Errors = null)
{
    public bool Succeeded => Status == LookupOperationStatus.Success;

    public static LookupOperationResult Success(Guid id) => new(LookupOperationStatus.Success, id);

    public static LookupOperationResult NotFound() => new(LookupOperationStatus.NotFound);

    public static LookupOperationResult Duplicate() => new(
        LookupOperationStatus.Duplicate,
        Errors: ["Já existe um cadastro com o mesmo nome ou código no escopo informado."]);

    public static LookupOperationResult Invalid(params string[] errors) => new(
        LookupOperationStatus.ValidationFailed,
        Errors: errors);
}
