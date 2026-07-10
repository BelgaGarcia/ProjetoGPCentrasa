using CentraSA.Domain.Common;
using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Entities;

public sealed class PendingTask : IConcurrencyTracked
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    public Person? ResponsiblePerson { get; set; }

    public Guid ResponsibleAreaId { get; set; }

    public TeamArea ResponsibleArea { get; set; } = null!;

    public Guid StatusDefinitionId { get; set; }

    public StatusDefinition StatusDefinition { get; set; } = null!;

    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public DateOnly? DueDate { get; set; }

    public string? Notes { get; set; }

    public Guid? CategoryId { get; set; }

    public Category? Category { get; set; }

    public string? Origin { get; set; }

    public int PresentationOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }

    public long Version { get; set; } = 1;

    public ICollection<WorkItemReference> References { get; } = new List<WorkItemReference>();
}
