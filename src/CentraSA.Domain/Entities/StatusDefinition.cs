using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Entities;

public sealed class StatusDefinition
{
    public Guid Id { get; set; }

    public WorkItemScope Scope { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public LifecycleState LifecycleState { get; set; }

    public string ColorToken { get; set; } = "blue";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
