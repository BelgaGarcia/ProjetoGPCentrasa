using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Domain.Rules;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.IntegrationTests;

public sealed class SqlitePersistenceTests
{
    [Fact]
    public async Task NewAndAlreadyMigratedDatabaseInitializationIsIdempotentAndPersistent()
    {
        string databasePath = CreateTemporaryDatabasePath();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(databasePath);

        await using (var firstContext = new CentraSaDbContext(options))
        {
            var initializer = new DatabaseInitializer(firstContext);
            await initializer.InitializeAsync(seedDemoData: true);

            Assert.Single(await firstContext.Database.GetAppliedMigrationsAsync());
            Assert.Equal(10, await firstContext.PendingTasks.CountAsync());
            Assert.Equal(7, await firstContext.Smuds.CountAsync());
            Assert.Equal(7, await firstContext.SupportTickets.CountAsync());
            Assert.Equal(6, await firstContext.WorkItemReferences.CountAsync());
            Assert.Equal(24, await firstContext.ActivityHistories.CountAsync());
            Assert.Empty(await firstContext.Database.GetPendingMigrationsAsync());
        }

        await using (var secondContext = new CentraSaDbContext(options))
        {
            var initializer = new DatabaseInitializer(secondContext);
            await initializer.InitializeAsync(seedDemoData: true);

            Assert.Equal(10, await secondContext.PendingTasks.CountAsync());
            Assert.Equal(7, await secondContext.Smuds.CountAsync());
            Assert.Equal(7, await secondContext.SupportTickets.CountAsync());
            Assert.Equal(17, await secondContext.StatusDefinitions.CountAsync());
            Assert.Equal(7, await secondContext.Categories.CountAsync());
            Assert.Single(await secondContext.Database.GetAppliedMigrationsAsync());
            Assert.Empty(await secondContext.Database.GetPendingMigrationsAsync());
        }
    }

    [Fact]
    public async Task UniqueSmudCodeIsEnforcedBySqlite()
    {
        string databasePath = CreateTemporaryDatabasePath();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(databasePath);

        await using var context = new CentraSaDbContext(options);
        await new DatabaseInitializer(context).InitializeAsync(seedDemoData: false);
        TeamArea area = await context.TeamAreas.FirstAsync();
        StatusDefinition status = await context.StatusDefinitions
            .FirstAsync(item => item.Scope == WorkItemScope.Smud);
        string normalizedCode = SmudCodeNormalizer.Normalize("SMUD84");

        context.Smuds.AddRange(
            CreateSmud(normalizedCode, "Primeiro SMUD", area.Id, status.Id),
            CreateSmud(normalizedCode, "Segundo SMUD", area.Id, status.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrencyVersionPreventsSilentOverwrite()
    {
        string databasePath = CreateTemporaryDatabasePath();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(databasePath);
        Guid taskId = Guid.NewGuid();

        await using (var setupContext = new CentraSaDbContext(options))
        {
            await new DatabaseInitializer(setupContext).InitializeAsync(seedDemoData: false);
            TeamArea area = await setupContext.TeamAreas.FirstAsync();
            StatusDefinition status = await setupContext.StatusDefinitions
                .FirstAsync(item => item.Scope == WorkItemScope.PendingTask);
            setupContext.PendingTasks.Add(new PendingTask
            {
                Id = taskId,
                Title = "Pendência concorrente",
                ResponsibleAreaId = area.Id,
                StatusDefinitionId = status.Id,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
            await setupContext.SaveChangesAsync();
        }

        await using var firstContext = new CentraSaDbContext(options);
        await using var secondContext = new CentraSaDbContext(options);
        PendingTask firstCopy = await firstContext.PendingTasks.SingleAsync(task => task.Id == taskId);
        PendingTask secondCopy = await secondContext.PendingTasks.SingleAsync(task => task.Id == taskId);

        firstCopy.Title = "Alteração da primeira aba";
        await firstContext.SaveChangesAsync();

        secondCopy.Title = "Alteração da segunda aba";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task ClosedDatabaseFileCanBeCopiedAndRestored()
    {
        string databasePath = CreateTemporaryDatabasePath();
        string directory = Path.GetDirectoryName(databasePath)!;
        string backupPath = Path.Combine(directory, "centrasa-manual-copy.db");
        DbContextOptions<CentraSaDbContext> options = CreateOptions(databasePath);
        Guid taskId = Guid.NewGuid();

        await using (var setupContext = new CentraSaDbContext(options))
        {
            await new DatabaseInitializer(setupContext).InitializeAsync(seedDemoData: false);
            TeamArea area = await setupContext.TeamAreas.FirstAsync();
            StatusDefinition status = await setupContext.StatusDefinitions
                .FirstAsync(item => item.Scope == WorkItemScope.PendingTask);
            setupContext.PendingTasks.Add(new PendingTask
            {
                Id = taskId,
                Title = "Estado incluído na cópia manual",
                ResponsibleAreaId = area.Id,
                StatusDefinitionId = status.Id,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
            await setupContext.SaveChangesAsync();
        }

        SqliteConnection.ClearAllPools();
        File.Copy(databasePath, backupPath);

        await using (var changedContext = new CentraSaDbContext(options))
        {
            PendingTask changedTask = await changedContext.PendingTasks.SingleAsync(task => task.Id == taskId);
            changedTask.Title = "Estado alterado depois da cópia";
            await changedContext.SaveChangesAsync();
        }

        SqliteConnection.ClearAllPools();
        File.Copy(backupPath, databasePath, overwrite: true);

        await using var restoredContext = new CentraSaDbContext(options);
        PendingTask restoredTask = await restoredContext.PendingTasks.SingleAsync(task => task.Id == taskId);
        Assert.Equal("Estado incluído na cópia manual", restoredTask.Title);
        Assert.Empty(await restoredContext.Database.GetPendingMigrationsAsync());
    }

    private static DbContextOptions<CentraSaDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<CentraSaDbContext>()
            .UseSqlite($"Data Source={databasePath};Foreign Keys=True")
            .Options;

    private static string CreateTemporaryDatabasePath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CentraSA.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "test.db");
    }

    private static Smud CreateSmud(string code, string title, Guid areaId, Guid statusId) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        NormalizedCode = code,
        Title = title,
        ResponsibleAreaId = areaId,
        StatusDefinitionId = statusId,
        RequiredAction = "Validar",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };
}
