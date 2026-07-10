namespace CentraSA.Application.Abstractions;

public interface IDatabaseInitializer
{
    Task InitializeAsync(bool seedDemoData, CancellationToken cancellationToken = default);
}
