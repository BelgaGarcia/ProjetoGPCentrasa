using CentraSA.Application;
using CentraSA.Domain;

namespace CentraSA.UnitTests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void DomainAssemblyHasExpectedName()
    {
        string? assemblyName = typeof(DomainAssemblyReference).Assembly.GetName().Name;

        Assert.Equal("CentraSA.Domain", assemblyName);
    }

    [Fact]
    public void ApplicationAssemblyReferencesDomain()
    {
        Type domainMarker = ApplicationAssemblyReference.DomainMarkerType;

        Assert.Equal(typeof(DomainAssemblyReference), domainMarker);
    }
}
