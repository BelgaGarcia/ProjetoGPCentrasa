using CentraSA.Application.Lookups;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Infrastructure.Repositories;
using CentraSA.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.IntegrationTests;

public sealed class LookupWorkflowTests
{
    [Fact]
    public async Task LookupsSupportCrudActivationAuditAndProtectCompletedStatus()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: false);
            var service = new LookupService(new LookupRepository(context), TimeProvider.System);
            Guid actorId = Guid.NewGuid();

            LookupOperationResult areaCreated = await service.CreateAsync(new LookupInput
            {
                Kind = LookupKind.TeamArea,
                Name = "  Controladoria  ",
                AreaKind = TeamAreaKind.InternalArea,
                ColorToken = "purple",
            }, actorId);
            Assert.True(areaCreated.Succeeded);
            Guid areaId = Assert.IsType<Guid>(areaCreated.Id);

            LookupOperationResult duplicate = await service.CreateAsync(new LookupInput
            {
                Kind = LookupKind.TeamArea,
                Name = "controladoria",
                AreaKind = TeamAreaKind.InternalArea,
                ColorToken = "blue",
            }, actorId);
            Assert.Equal(LookupOperationStatus.ValidationFailed, duplicate.Status);

            LookupOperationResult personCreated = await service.CreateAsync(new LookupInput
            {
                Kind = LookupKind.Person,
                Name = "Pessoa de Teste",
                TeamAreaId = areaId,
            }, actorId);
            Assert.True(personCreated.Succeeded);

            LookupOperationResult disabled = await service.ToggleActiveAsync(
                LookupKind.TeamArea,
                areaId,
                activate: false,
                actorId);
            Assert.True(disabled.Succeeded);
            LookupOperationResult enabled = await service.ToggleActiveAsync(
                LookupKind.TeamArea,
                areaId,
                activate: true,
                actorId);
            Assert.True(enabled.Succeeded);

            Guid completedStatusId = await context.StatusDefinitions
                .Where(item => item.Scope == WorkItemScope.PendingTask && item.LifecycleState == LifecycleState.Completed)
                .Select(item => item.Id)
                .SingleAsync();
            LookupOperationResult completedDisabled = await service.ToggleActiveAsync(
                LookupKind.Status,
                completedStatusId,
                activate: false,
                actorId);
            Assert.Equal(LookupOperationStatus.ValidationFailed, completedDisabled.Status);

            LookupOperationResult duplicateCompleted = await service.CreateAsync(new LookupInput
            {
                Kind = LookupKind.Status,
                Name = "Outra conclusão",
                Code = "outra-conclusao",
                Scope = WorkItemScope.PendingTask,
                LifecycleState = LifecycleState.Completed,
                ColorToken = "green",
                SortOrder = 60,
            }, actorId);
            Assert.Equal(LookupOperationStatus.ValidationFailed, duplicateCompleted.Status);

            LookupOperationResult invalidSmudCategory = await service.CreateAsync(new LookupInput
            {
                Kind = LookupKind.Category,
                Name = "Categoria indevida",
                Code = "categoria-indevida",
                Scope = WorkItemScope.Smud,
                ColorToken = "blue",
            }, actorId);
            Assert.Equal(LookupOperationStatus.ValidationFailed, invalidSmudCategory.Status);

            LookupOverviewData overview = await service.GetOverviewAsync();
            Assert.Contains(overview.Areas, item => item.Id == areaId && item.IsActive && item.Name == "Controladoria");
            Assert.Contains(overview.People, item => item.Id == personCreated.Id && item.Context == "Controladoria");

            var history = await context.ActivityHistories.AsNoTracking()
                .Where(item => item.EntityType == TrackedEntityType.TeamArea && item.EntityId == areaId)
                .OrderBy(item => item.OccurredAtUtc)
                .ToListAsync();
            Assert.Contains(history, item => item.ActionType == ActivityActionType.Created);
            Assert.Contains(history, item => item.ActionType == ActivityActionType.Archived);
            Assert.Contains(history, item => item.ActionType == ActivityActionType.Restored);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CentraSA.LookupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static DbContextOptions<CentraSaDbContext> CreateOptions(string directory) =>
        new DbContextOptionsBuilder<CentraSaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "test.db")};Foreign Keys=True")
            .Options;
}
