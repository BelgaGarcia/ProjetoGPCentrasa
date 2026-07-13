using CentraSA.Application.SupportTickets;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Infrastructure.Repositories;
using CentraSA.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.IntegrationTests;

public sealed class SupportTicketWorkflowTests
{
    private static readonly DateOnly ReferenceDate = new(2026, 7, 13);

    [Fact]
    public async Task SevenDemoTicketsComposeCardResponsibleAndDeadlineWithIndependentFilters()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: true);
            var service = new SupportTicketService(new SupportTicketRepository(context), TimeProvider.System);

            SupportTicketBoardData board = await service.SearchAsync(CreateSearch());
            List<SupportTicketBoardCard> cards = board.Groups.SelectMany(group => group.Cards).ToList();

            Assert.Equal(7, board.TotalCount);
            Assert.Equal(board.TotalCount, cards.Count);
            Assert.Equal(
                ["14779", "14896", "14899", "14973", "14995", "15052", "15056"],
                cards.Select(card => card.Number).OrderBy(number => number).ToArray());
            Assert.DoesNotContain(cards, card => card.Title.Contains("fiscal", StringComparison.OrdinalIgnoreCase));
            Assert.All(cards, card =>
            {
                Assert.False(string.IsNullOrWhiteSpace(card.Area));
                Assert.True(card.DueDate.HasValue);
                Assert.True(card.RequiresAction);
            });

            foreach (SupportTicketBoardGroup group in board.Groups)
            {
                Assert.All(group.Cards, card => Assert.Equal(group.Category, card.Category));
                Assert.Equal(group.Cards.Count, group.Total);
            }

            Guid incidentCategoryId = board.Options.Categories.Single(category => category.Name == "Incidente").Id;
            Guid supplierAreaId = board.Options.Areas.Single(area => area.Name == "Fornecedor Beta").Id;
            SupportTicketBoardData categoryFiltered = await service.SearchAsync(CreateSearch(categoryId: incidentCategoryId));
            SupportTicketBoardData areaFiltered = await service.SearchAsync(CreateSearch(areaId: supplierAreaId));

            Assert.Equal(3, categoryFiltered.TotalCount);
            Assert.All(categoryFiltered.Groups.SelectMany(group => group.Cards), card => Assert.Equal("Incidente", card.Category));
            Assert.Equal(3, areaFiltered.TotalCount);
            Assert.All(areaFiltered.Groups.SelectMany(group => group.Cards), card => Assert.Equal("Fornecedor Beta", card.Area));

            SupportTicketBoardData overdue = await service.SearchAsync(
                CreateSearch(dueFilter: SupportTicketDueFilter.Overdue, today: ReferenceDate.AddDays(1)));
            Assert.Equal(5, overdue.TotalCount);
            Assert.All(overdue.Groups.SelectMany(group => group.Cards), card => Assert.True(card.IsOverdue));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CrudRejectsDuplicateKeepsCategoryIndependentAndSupportsArchive()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: false);
            var repository = new SupportTicketRepository(context);
            var service = new SupportTicketService(repository, TimeProvider.System);
            SupportTicketReferenceData references = await repository.GetReferenceDataAsync();
            Guid actorId = Guid.NewGuid();
            Guid categoryId = references.Categories[0].Id;
            Guid areaId = references.Areas[^1].Id;
            Guid statusId = references.Statuses.First(status => status.LifecycleState == LifecycleState.Active).Id;

            SupportTicketOperationResult created = await service.CreateAsync(
                CreateInput(" 16001 ", categoryId, areaId, statusId),
                actorId);

            Assert.True(created.Succeeded);
            Guid ticketId = Assert.IsType<Guid>(created.Id);
            SupportTicketDetailsData details = Assert.IsType<SupportTicketDetailsData>(
                await service.GetDetailsAsync(ticketId, includeArchived: false));
            Assert.Equal("16001", details.Item.Number);
            Assert.Equal(references.Categories[0].Name, details.Item.Category);
            Assert.Equal(references.Areas[^1].Name, details.Item.Area);

            SupportTicketOperationResult duplicate = await service.CreateAsync(
                CreateInput("16001", references.Categories[1].Id, references.Areas[0].Id, statusId),
                actorId);
            Assert.Equal(SupportTicketOperationStatus.DuplicateNumber, duplicate.Status);

            SupportTicketEditorData editor = Assert.IsType<SupportTicketEditorData>(
                await service.GetEditEditorAsync(ticketId));
            editor.Input.CategoryId = references.Categories[1].Id;
            SupportTicketOperationResult updated = await service.UpdateAsync(ticketId, editor.Input, actorId);
            Assert.True(updated.Succeeded);

            SupportTicketDetailsData updatedDetails = Assert.IsType<SupportTicketDetailsData>(
                await service.GetDetailsAsync(ticketId, includeArchived: false));
            Assert.Equal(references.Categories[1].Name, updatedDetails.Item.Category);
            Assert.Equal(references.Areas[^1].Name, updatedDetails.Item.Area);

            SupportTicketOperationResult archived = await service.ArchiveAsync(
                ticketId,
                updatedDetails.Item.Version,
                actorId);
            Assert.True(archived.Succeeded);

            SupportTicketBoardData archivedBoard = await service.SearchAsync(
                CreateSearch(archivedOnly: true, hideFinalized: false));
            Assert.Equal("16001", Assert.Single(archivedBoard.Groups.SelectMany(group => group.Cards)).Number);

            SupportTicketDetailsData archivedDetails = Assert.IsType<SupportTicketDetailsData>(
                await service.GetDetailsAsync(ticketId, includeArchived: true));
            Assert.Contains(archivedDetails.History, history => history.Action == "Arquivamento");

            SupportTicketOperationResult restored = await service.RestoreAsync(
                ticketId,
                archivedDetails.Item.Version,
                actorId);
            Assert.True(restored.Succeeded);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TicketConnectorCssUsesResponsiveGridWithoutAbsoluteCoordinates()
    {
        string repositoryRoot = FindRepositoryRoot();
        string css = File.ReadAllText(Path.Combine(repositoryRoot, "src", "CentraSA.Web", "wwwroot", "css", "pages.css"));
        int ticketRulesStart = css.IndexOf(".ticket-filters", StringComparison.Ordinal);
        int firstResponsiveRule = css.IndexOf("@media (max-width: 1199.98px)", ticketRulesStart, StringComparison.Ordinal);
        string ticketRules = css[ticketRulesStart..firstResponsiveRule];

        Assert.Contains(".ticket-relation", ticketRules, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns", ticketRules, StringComparison.Ordinal);
        Assert.Contains(".ticket-connector::before", ticketRules, StringComparison.Ordinal);
        Assert.DoesNotContain("position: absolute", ticketRules, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 767.98px)", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: minmax(0, 1fr)", css, StringComparison.Ordinal);
    }

    private static SupportTicketSearch CreateSearch(
        Guid? categoryId = null,
        Guid? areaId = null,
        SupportTicketDueFilter dueFilter = SupportTicketDueFilter.All,
        DateOnly? today = null,
        bool archivedOnly = false,
        bool hideFinalized = true) => new(
        Search: null,
        CategoryId: categoryId,
        AreaId: areaId,
        PersonId: null,
        StatusId: null,
        DueFilter: dueFilter,
        ActionRequiredOnly: false,
        HideFinalized: hideFinalized,
        ArchivedOnly: archivedOnly,
        Today: today ?? ReferenceDate);

    private static SupportTicketInput CreateInput(
        string number,
        Guid categoryId,
        Guid areaId,
        Guid statusId) => new()
        {
            Number = number,
            Title = "Falha demonstrativa",
            CategoryId = categoryId,
            ResponsibleAreaId = areaId,
            StatusId = statusId,
            Priority = PriorityLevel.High,
            OpenedOn = ReferenceDate,
            DueDate = ReferenceDate.AddDays(7),
            PendingAction = "Acompanhar retorno",
            Version = 1,
        };

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CentraSA.SupportTicketTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static DbContextOptions<CentraSaDbContext> CreateOptions(string directory) =>
        new DbContextOptionsBuilder<CentraSaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "test.db")};Foreign Keys=True")
            .Options;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CentraSA.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("A raiz do repositório não foi encontrada.");
    }
}
