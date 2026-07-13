using CentraSA.Application.Smuds;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Infrastructure.Repositories;
using CentraSA.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.IntegrationTests;

public sealed class SmudWorkflowTests
{
    private static readonly DateOnly ReferenceDate = new(2026, 7, 13);

    [Fact]
    public async Task DemoExamplesAreGroupedByPersistedStatusAndFilteredTotalsMatchCards()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: true);
            var service = new SmudService(new SmudRepository(context), TimeProvider.System);

            SmudBoardData board = await service.SearchAsync(CreateSearch());
            List<SmudBoardCard> cards = board.Columns.SelectMany(column => column.Cards).ToList();

            Assert.Equal(7, board.TotalCount);
            Assert.Equal(board.TotalCount, cards.Count);
            Assert.DoesNotContain(cards, card => card.Title.Contains("fiscal", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(
                ["SMUD077", "SMUD081", "SMUD083", "SMUD084", "SMUD085"],
                cards.Where(card => card.Code is "SMUD077" or "SMUD081" or "SMUD083" or "SMUD084" or "SMUD085")
                    .Select(card => card.Code)
                    .OrderBy(code => code)
                    .ToArray());

            foreach (SmudBoardColumn column in board.Columns)
            {
                Assert.All(column.Cards, card => Assert.Equal(column.Status, card.Status));
                Assert.Equal(column.Cards.Count, column.Total);
            }

            SmudBoardCard overdue = Assert.Single(cards, card => card.IsOverdue);
            Assert.Equal("SMUD083", overdue.Code);
            Assert.True(overdue.RequiresAction);

            Guid supplierStatusId = board.Options.Statuses.Single(status => status.Name == "Pendente fornecedor").Id;
            SmudBoardData statusFiltered = await service.SearchAsync(CreateSearch(statusId: supplierStatusId));
            SmudBoardColumn statusColumn = Assert.Single(statusFiltered.Columns);
            Assert.Equal(5, statusFiltered.TotalCount);
            Assert.Equal(statusFiltered.TotalCount, statusColumn.Cards.Count);

            SmudBoardData overdueFiltered = await service.SearchAsync(
                CreateSearch(dueFilter: SmudDueFilter.Overdue));
            Assert.Equal(1, overdueFiltered.TotalCount);
            Assert.Equal("SMUD083", Assert.Single(overdueFiltered.Columns.SelectMany(column => column.Cards)).Code);

            SmudBoardData actionFiltered = await service.SearchAsync(CreateSearch(actionRequiredOnly: true));
            Assert.Equal(actionFiltered.TotalCount, actionFiltered.Columns.Sum(column => column.Cards.Count));
            Assert.All(actionFiltered.Columns.SelectMany(column => column.Cards), card => Assert.True(card.RequiresAction));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CrudNormalizesCodeRejectsDuplicateAndTracksStatusAndArchiveHistory()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: false);
            var repository = new SmudRepository(context);
            var service = new SmudService(repository, TimeProvider.System);
            SmudReferenceData references = await repository.GetReferenceDataAsync();
            Guid actorId = Guid.NewGuid();
            Guid activeStatusId = references.Statuses.First(status => status.LifecycleState == LifecycleState.Active).Id;
            Guid completedStatusId = references.Statuses.First(status => status.LifecycleState == LifecycleState.Completed).Id;

            SmudOperationResult created = await service.CreateAsync(
                CreateInput(" smud-84 ", references.Areas[0].Id, activeStatusId),
                actorId);

            Assert.True(created.Succeeded);
            Guid smudId = Assert.IsType<Guid>(created.Id);
            SmudDetailsData createdDetails = Assert.IsType<SmudDetailsData>(
                await service.GetDetailsAsync(smudId, includeArchived: false));
            Assert.Equal("SMUD084", createdDetails.Item.Code);
            Assert.Contains(createdDetails.History, history => history.Action == "Criação");

            SmudOperationResult duplicate = await service.CreateAsync(
                CreateInput("SMUD084", references.Areas[0].Id, activeStatusId),
                actorId);
            Assert.Equal(SmudOperationStatus.DuplicateCode, duplicate.Status);

            SmudEditorData editor = Assert.IsType<SmudEditorData>(await service.GetEditEditorAsync(smudId));
            editor.Input.StatusId = completedStatusId;
            editor.Input.RequiredAction = null;
            SmudOperationResult updated = await service.UpdateAsync(smudId, editor.Input, actorId);
            Assert.True(updated.Succeeded);

            SmudDetailsData completedDetails = Assert.IsType<SmudDetailsData>(
                await service.GetDetailsAsync(smudId, includeArchived: false));
            Assert.Equal(LifecycleState.Completed, completedDetails.Item.LifecycleState);
            Assert.NotNull(completedDetails.CompletedAtUtc);
            Assert.Contains(completedDetails.History, history => history.Action == "Mudança de status");
            Assert.Contains(completedDetails.History, history => history.Action == "Conclusão");

            SmudOperationResult archived = await service.ArchiveAsync(
                smudId,
                completedDetails.Item.Version,
                actorId);
            Assert.True(archived.Succeeded);

            SmudDetailsData archivedDetails = Assert.IsType<SmudDetailsData>(
                await service.GetDetailsAsync(smudId, includeArchived: true));
            Assert.NotNull(archivedDetails.ArchivedAtUtc);
            Assert.Contains(archivedDetails.History, history => history.Action == "Arquivamento");

            SmudOperationResult restored = await service.RestoreAsync(
                smudId,
                archivedDetails.Item.Version,
                actorId);
            Assert.True(restored.Succeeded);

            SmudDetailsData restoredDetails = Assert.IsType<SmudDetailsData>(
                await service.GetDetailsAsync(smudId, includeArchived: false));
            Assert.Null(restoredDetails.ArchivedAtUtc);
            Assert.Contains(restoredDetails.History, history => history.Action == "Restauração");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static SmudSearch CreateSearch(
        Guid? statusId = null,
        SmudDueFilter dueFilter = SmudDueFilter.All,
        bool actionRequiredOnly = false) => new(
        Search: null,
        AreaId: null,
        PersonId: null,
        StatusId: statusId,
        DueFilter: dueFilter,
        ActionRequiredOnly: actionRequiredOnly,
        HideFinalized: true,
        ArchivedOnly: false,
        Today: ReferenceDate);

    private static SmudInput CreateInput(string code, Guid areaId, Guid statusId) => new()
    {
        Code = code,
        Title = "Validação demonstrativa",
        ResponsibleAreaId = areaId,
        StatusId = statusId,
        Priority = PriorityLevel.High,
        OpenedOn = ReferenceDate,
        DueDate = ReferenceDate.AddDays(7),
        RequiredAction = "Validar desenvolvimento",
        Version = 1,
    };

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CentraSA.SmudTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static DbContextOptions<CentraSaDbContext> CreateOptions(string directory) =>
        new DbContextOptionsBuilder<CentraSaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "test.db")};Foreign Keys=True")
            .Options;
}
