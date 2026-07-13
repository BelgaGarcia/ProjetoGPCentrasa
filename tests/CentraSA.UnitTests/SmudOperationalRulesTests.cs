using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;

namespace CentraSA.UnitTests;

public sealed class SmudOperationalRulesTests
{
    [Fact]
    public void ActiveSmudWithDescriptionRequiresAction()
    {
        Assert.True(SmudOperationalRules.RequiresAction(
            "Validar desenvolvimento",
            LifecycleState.Active,
            archivedAtUtc: null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyActionDoesNotRequireAction(string? requiredAction)
    {
        Assert.False(SmudOperationalRules.RequiresAction(
            requiredAction,
            LifecycleState.Active,
            archivedAtUtc: null));
    }

    [Theory]
    [InlineData(LifecycleState.Completed)]
    [InlineData(LifecycleState.Cancelled)]
    public void FinalizedSmudDoesNotRequireAction(LifecycleState lifecycleState)
    {
        Assert.False(SmudOperationalRules.RequiresAction(
            "Executar próximo passo",
            lifecycleState,
            archivedAtUtc: null));
    }

    [Fact]
    public void ArchivedSmudDoesNotRequireAction()
    {
        Assert.False(SmudOperationalRules.RequiresAction(
            "Executar próximo passo",
            LifecycleState.Active,
            new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc)));
    }
}
