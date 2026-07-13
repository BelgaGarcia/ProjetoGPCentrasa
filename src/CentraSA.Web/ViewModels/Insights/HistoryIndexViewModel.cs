using CentraSA.Application.Insights;
using CentraSA.Domain.Enums;

namespace CentraSA.Web.ViewModels.Insights;

public sealed class HistoryIndexViewModel
{
    public string? Search { get; set; }

    public TrackedEntityType? EntityType { get; set; }

    public ActivityActionType? ActionType { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public int Page { get; set; } = 1;

    public GlobalHistoryPage Data { get; set; } = null!;
}
