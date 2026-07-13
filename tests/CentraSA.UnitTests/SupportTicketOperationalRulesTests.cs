using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;

namespace CentraSA.UnitTests;

public sealed class SupportTicketOperationalRulesTests
{
    [Fact]
    public void ActiveTicketWithPendingDescriptionRequiresAction()
    {
        Assert.True(SupportTicketOperationalRules.RequiresAction(
            "Acompanhar retorno",
            LifecycleState.Active,
            archivedAtUtc: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPendingActionDoesNotRequireAction(string? pendingAction)
    {
        Assert.False(SupportTicketOperationalRules.RequiresAction(
            pendingAction,
            LifecycleState.Active,
            archivedAtUtc: null));
    }

    [Theory]
    [InlineData(LifecycleState.Completed)]
    [InlineData(LifecycleState.Cancelled)]
    public void FinalizedTicketDoesNotRequireAction(LifecycleState lifecycleState)
    {
        Assert.False(SupportTicketOperationalRules.RequiresAction(
            "Acompanhar retorno",
            lifecycleState,
            archivedAtUtc: null));
    }

    [Fact]
    public void ArchivedTicketDoesNotRequireAction()
    {
        Assert.False(SupportTicketOperationalRules.RequiresAction(
            "Acompanhar retorno",
            LifecycleState.Active,
            new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc)));
    }
}
