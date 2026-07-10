namespace CentraSA.Web.ViewModels.Components;

public sealed record PageHeaderViewModel(
    string Title,
    string? Description = null,
    string? Eyebrow = null,
    string? ActionLabel = null,
    string? ActionUrl = null,
    string ActionIcon = "plus");
