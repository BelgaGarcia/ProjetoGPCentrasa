using CentraSA.Domain.Enums;

namespace CentraSA.Domain.Rules;

public static class SupportTicketOperationalRules
{
    public static bool RequiresAction(
        string? pendingAction,
        LifecycleState lifecycleState,
        DateTime? archivedAtUtc) =>
        !string.IsNullOrWhiteSpace(pendingAction)
        && lifecycleState == LifecycleState.Active
        && archivedAtUtc is null;
}
