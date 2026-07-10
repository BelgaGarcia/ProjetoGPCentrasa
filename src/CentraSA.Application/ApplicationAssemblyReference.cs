using CentraSA.Domain;

namespace CentraSA.Application;

/// <summary>
/// Fornece tipos estáveis para localizar os assemblies da aplicação e domínio.
/// </summary>
public static class ApplicationAssemblyReference
{
    public static Type DomainMarkerType => typeof(DomainAssemblyReference);
}
