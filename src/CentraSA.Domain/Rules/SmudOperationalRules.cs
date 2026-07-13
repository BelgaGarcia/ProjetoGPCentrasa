using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Rules;

public static class SmudOperationalRules
{
    public static bool RequiresAction(
        string? requiredAction,
        LifecycleState lifecycleState,
        DateTime? archivedAtUtc) =>
        !string.IsNullOrWhiteSpace(requiredAction)
        && lifecycleState == LifecycleState.Active
        && archivedAtUtc is null;
}
