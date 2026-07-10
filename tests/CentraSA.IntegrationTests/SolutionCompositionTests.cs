using CentraSA.Infrastructure;
using CentraSA.Web.Controllers;

namespace CentraSA.IntegrationTests;

public sealed class SolutionCompositionTests
{
    [Fact]
    public void WebAndInfrastructureAssembliesAreAvailableToIntegrationTests()
    {
        string? webAssembly = typeof(HomeController).Assembly.GetName().Name;
        string? infrastructureAssembly = typeof(InfrastructureAssemblyReference).Assembly.GetName().Name;

        Assert.Equal("CentraSA.Web", webAssembly);
        Assert.Equal("CentraSA.Infrastructure", infrastructureAssembly);
    }
}
