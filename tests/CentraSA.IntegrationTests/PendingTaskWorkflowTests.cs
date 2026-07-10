using CentraSA.Application.PendingTasks;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Infrastructure.Repositories;
using CentraSA.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.IntegrationTests;

public sealed class PendingTaskWorkflowTests
{
    [Fact]
    public async Task CreateCompleteReopenAndArchivePersistWithHistory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CentraSA.PendingTaskTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string databasePath = Path.Combine(directory, "test.db");
        var options = new DbContextOptionsBuilder<CentraSaDbContext>()
            .UseSqlite($"Data Source={databasePath};Foreign Keys=True")
            .Options;

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: false);
            var repository = new PendingTaskRepository(context);
            var service = new PendingTaskService(repository, TimeProvider.System);
            PendingTaskReferenceData references = await repository.GetReferenceDataAsync();
            Guid actorId = Guid.NewGuid();

            PendingTaskOperationResult created = await service.CreateAsync(
                new PendingTaskInput
                {
                    Title = "Validar fechamento fiscal",
                    ResponsibleAreaId = references.Areas[0].Id,
                    StatusId = references.Statuses.First(status => status.LifecycleState == LifecycleState.Active).Id,
                    CategoryId = references.Categories.Count > 0 ? references.Categories[0].Id : null,
                    Priority = PriorityLevel.High,
                    DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                },
                actorId);

            Assert.True(created.Succeeded);
            Guid taskId = Assert.IsType<Guid>(created.Id);
            PendingTaskDetailsData createdDetails = Assert.IsType<PendingTaskDetailsData>(
                await service.GetDetailsAsync(taskId, includeArchived: false));
            Assert.Contains(createdDetails.History, history => history.Action == "Criação");

            PendingTaskOperationResult completed = await service.CompleteAsync(
                taskId,
                createdDetails.Item.Version,
                actorId);
            Assert.True(completed.Succeeded);

            PendingTaskDetailsData completedDetails = Assert.IsType<PendingTaskDetailsData>(
                await service.GetDetailsAsync(taskId, includeArchived: false));
            Assert.NotNull(completedDetails.Item.CompletedAtUtc);

            PendingTaskOperationResult reopened = await service.ReopenAsync(
                taskId,
                completedDetails.Item.Version,
                actorId);
            Assert.True(reopened.Succeeded);

            PendingTaskDetailsData reopenedDetails = Assert.IsType<PendingTaskDetailsData>(
                await service.GetDetailsAsync(taskId, includeArchived: false));
            Assert.Null(reopenedDetails.Item.CompletedAtUtc);

            PendingTaskOperationResult archived = await service.ArchiveAsync(
                taskId,
                reopenedDetails.Item.Version,
                actorId);
            Assert.True(archived.Succeeded);

            PendingTaskDetailsData archivedDetails = Assert.IsType<PendingTaskDetailsData>(
                await service.GetDetailsAsync(taskId, includeArchived: true));
            Assert.NotNull(archivedDetails.ArchivedAtUtc);
            Assert.Contains(archivedDetails.History, history => history.Action == "Conclusão");
            Assert.Contains(archivedDetails.History, history => history.Action == "Reabertura");
            Assert.Contains(archivedDetails.History, history => history.Action == "Arquivamento");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}
