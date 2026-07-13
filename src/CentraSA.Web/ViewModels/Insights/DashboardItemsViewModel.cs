using CentraSA.Application.Insights;
using CentraSA.Domain.Enums;

namespace CentraSA.Web.ViewModels.Insights;

public sealed class DashboardItemsViewModel
{
    public string? Search { get; set; }

    public TrackedEntityType? SourceType { get; set; }

    public DashboardItemStateFilter State { get; set; }

    public DashboardItemListData Data { get; set; } = null!;
}
