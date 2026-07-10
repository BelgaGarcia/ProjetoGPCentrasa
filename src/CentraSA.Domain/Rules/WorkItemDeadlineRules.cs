using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Rules;

public static class WorkItemDeadlineRules
{
    public static bool IsOverdue(
        DateOnly? dueDate,
        LifecycleState lifecycleState,
        DateTime? archivedAtUtc,
        DateOnly today) =>
        dueDate.HasValue
        && dueDate.Value < today
        && lifecycleState == LifecycleState.Active
        && archivedAtUtc is null;

    public static bool IsDueSoon(
        DateOnly? dueDate,
        LifecycleState lifecycleState,
        DateTime? archivedAtUtc,
        DateOnly today) =>
        dueDate.HasValue
        && dueDate.Value >= today
        && dueDate.Value <= today.AddDays(7)
        && lifecycleState == LifecycleState.Active
        && archivedAtUtc is null;
}
