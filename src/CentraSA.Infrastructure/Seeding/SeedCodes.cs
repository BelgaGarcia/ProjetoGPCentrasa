namespace CentraSA.Infrastructure.Seeding;

internal static class SeedCodes
{
    public const string PendingOpen = "OPEN";
    public const string PendingInProgress = "IN_PROGRESS";
    public const string PendingBlocked = "BLOCKED";
    public const string PendingCompleted = "COMPLETED";
    public const string PendingCancelled = "CANCELLED";

    public const string SmudPendingCentraSa = "PENDING_CENTRASA";
    public const string SmudPendingSupplier = "PENDING_SUPPLIER";
    public const string SmudAwaitingValidation = "AWAITING_VALIDATION";
    public const string SmudInDevelopment = "IN_DEVELOPMENT";
    public const string SmudCompleted = "COMPLETED";
    public const string SmudCancelled = "CANCELLED";

    public const string TicketOpen = "OPEN";
    public const string TicketInProgress = "IN_PROGRESS";
    public const string TicketAwaitingExternal = "AWAITING_EXTERNAL";
    public const string TicketAwaitingTests = "AWAITING_TESTS";
    public const string TicketCompleted = "COMPLETED";
    public const string TicketCancelled = "CANCELLED";

    public const string PendingOperational = "OPERATIONAL";
    public const string PendingValidation = "VALIDATION";
    public const string PendingReport = "REPORT";

    public const string TicketIncident = "INCIDENT";
    public const string TicketObservation = "OBSERVATION";
    public const string TicketPendingInternalTests = "PENDING_INTERNAL_TESTS";
    public const string TicketRequest = "REQUEST";
}
