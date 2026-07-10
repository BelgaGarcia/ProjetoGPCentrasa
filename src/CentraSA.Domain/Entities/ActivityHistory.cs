using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Entities;

public sealed class ActivityHistory
{
    public Guid Id { get; set; }

    public TrackedEntityType EntityType { get; set; }

    public Guid EntityId { get; set; }

    public ActivityActionType ActionType { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public Guid? ActorUserId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? ChangesJson { get; set; }
}
