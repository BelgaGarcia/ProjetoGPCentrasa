using CentraSA.Application.Abstractions;
using CentraSA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Seeding;

public sealed class DatabaseInitializer(CentraSaDbContext dbContext) : IDatabaseInitializer
{
    public async Task InitializeAsync(bool seedDemoData, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await ReferenceDataSeeder.SeedAsync(dbContext, cancellationToken);

        if (seedDemoData)
        {
            await DemoDataSeeder.SeedAsync(dbContext, cancellationToken);
        }
    }
}
