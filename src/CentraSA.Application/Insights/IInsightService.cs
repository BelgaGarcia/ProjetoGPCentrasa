using CentraSA.Domain.Enums;

namespace CentraSA.Application.Insights;

public interface IInsightService
{
    Task<DashboardData> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<DashboardItemListData> SearchItemsAsync(
        string? search,
        TrackedEntityType? sourceType,
        DashboardItemStateFilter state,
        CancellationToken cancellationToken = default);

    Task<GlobalHistoryPage> SearchHistoryAsync(
        GlobalHistorySearch search,
        CancellationToken cancellationToken = default);

    Task<HistoryDetailsData?> GetHistoryDetailsAsync(
        TrackedEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);
}
