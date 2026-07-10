using CentraSA.Application;
using CentraSA.Domain;

namespace CentraSA.Infrastructure;

/// <summary>
/// Fornece tipos estáveis para localizar os assemblies de infraestrutura.
/// </summary>
public static class InfrastructureAssemblyReference
{
    public static Type ApplicationMarkerType => typeof(ApplicationAssemblyReference);

    public static Type DomainMarkerType => typeof(DomainAssemblyReference);
}
