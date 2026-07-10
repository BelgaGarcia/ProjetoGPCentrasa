using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;

namespace CentraSA.UnitTests;

public sealed class WorkItemDeadlineRulesTests
{
    private static readonly DateOnly Today = new(2026, 7, 10);

    [Fact]
    public void ActiveItemBeforeTodayIsOverdue()
    {
        bool result = WorkItemDeadlineRules.IsOverdue(
            Today.AddDays(-1),
            LifecycleState.Active,
            archivedAtUtc: null,
            Today);

        Assert.True(result);
    }

    [Theory]
    [InlineData(LifecycleState.Completed)]
    [InlineData(LifecycleState.Cancelled)]
    public void NonActiveItemIsExcludedFromDeadlineAlerts(LifecycleState lifecycleState)
    {
        Assert.False(WorkItemDeadlineRules.IsOverdue(Today.AddDays(-1), lifecycleState, null, Today));
        Assert.False(WorkItemDeadlineRules.IsDueSoon(Today.AddDays(1), lifecycleState, null, Today));
    }

    [Fact]
    public void ArchivedItemIsExcludedFromDeadlineAlerts()
    {
        DateTime archivedAtUtc = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(WorkItemDeadlineRules.IsOverdue(Today.AddDays(-1), LifecycleState.Active, archivedAtUtc, Today));
        Assert.False(WorkItemDeadlineRules.IsDueSoon(Today.AddDays(1), LifecycleState.Active, archivedAtUtc, Today));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(-1, false)]
    public void DueSoonWindowIsInclusive(int daysFromToday, bool expected)
    {
        bool result = WorkItemDeadlineRules.IsDueSoon(
            Today.AddDays(daysFromToday),
            LifecycleState.Active,
            archivedAtUtc: null,
            Today);

        Assert.Equal(expected, result);
    }
}
