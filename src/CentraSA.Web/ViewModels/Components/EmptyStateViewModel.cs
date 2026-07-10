namespace CentraSA.Web.ViewModels.Components;

public sealed record EmptyStateViewModel(
    string Title,
    string Description,
    string Icon = "inbox",
    string? ActionLabel = null,
    string? ActionUrl = null);
