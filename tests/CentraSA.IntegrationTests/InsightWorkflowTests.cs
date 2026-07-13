using CentraSA.Application.Insights;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Infrastructure.Repositories;
using CentraSA.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.IntegrationTests;

public sealed class InsightWorkflowTests
{
    private static readonly DateTimeOffset ReferenceNow = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(ReferenceNow.UtcDateTime);

    [Fact]
    public void InsightQueriesHaveDeadlineAndHistoryIndexesInTheEfModel()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            using var context = new CentraSaDbContext(CreateOptions(directory));
            Assert.True(HasIndex(context, typeof(PendingTask), "ArchivedAtUtc", "StatusDefinitionId", "DueDate"));
            Assert.True(HasIndex(context, typeof(Smud), "ArchivedAtUtc", "StatusDefinitionId", "DueDate"));
            Assert.True(HasIndex(context, typeof(SupportTicket), "ArchivedAtUtc", "StatusDefinitionId", "DueDate"));
            Assert.True(HasIndex(context, typeof(ActivityHistory), "EntityType", "EntityId", "OccurredAtUtc"));
            Assert.True(HasIndex(context, typeof(ActivityHistory), "OccurredAtUtc"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DashboardCountersAndDrillDownRespectDeadlineBoundaries()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: false);
            TeamArea area = await context.TeamAreas.FirstAsync();
            StatusDefinition pendingActive = await ActiveStatusAsync(context, WorkItemScope.PendingTask);
            StatusDefinition pendingCompleted = await CompletedStatusAsync(context, WorkItemScope.PendingTask);
            StatusDefinition smudActive = await ActiveStatusAsync(context, WorkItemScope.Smud);
            StatusDefinition ticketActive = await ActiveStatusAsync(context, WorkItemScope.SupportTicket);
            Category ticketCategory = await context.Categories.FirstAsync(item => item.Scope == WorkItemScope.SupportTicket);
            DateTime now = ReferenceNow.UtcDateTime;

            PendingTask overdueTask = CreatePending("Pendência atrasada", Today.AddDays(-1), pendingActive.Id, area.Id, now);
            PendingTask dueTodayTask = CreatePending("Pendência de hoje", Today, pendingActive.Id, area.Id, now);
            PendingTask seventhDayTask = CreatePending("Pendência no sétimo dia", Today.AddDays(7), pendingActive.Id, area.Id, now);
            PendingTask eighthDayTask = CreatePending("Pendência no oitavo dia", Today.AddDays(8), pendingActive.Id, area.Id, now);
            PendingTask recentCompleted = CreatePending("Pendência concluída no limite", Today.AddDays(-10), pendingCompleted.Id, area.Id, now);
            recentCompleted.CompletedAtUtc = now.AddDays(-7);
            var smud = new Smud
            {
                Id = Guid.NewGuid(),
                Code = "SMUD900",
                NormalizedCode = "SMUD900",
                Title = "SMUD de hoje",
                ResponsibleAreaId = area.Id,
                StatusDefinitionId = smudActive.Id,
                DueDate = Today,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            var ticket = new SupportTicket
            {
                Id = Guid.NewGuid(),
                TicketNumber = "99001",
                NormalizedNumber = "99001",
                Title = "Chamado atrasado",
                CategoryId = ticketCategory.Id,
                ResponsibleAreaId = area.Id,
                StatusDefinitionId = ticketActive.Id,
                OpenedOn = Today.AddDays(-2),
                DueDate = Today.AddDays(-1),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            var meeting = new DailyMeeting
            {
                Id = Guid.NewGuid(),
                MeetingDate = Today,
                StartedAtUtc = now,
                Status = MeetingStatus.Draft,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            context.AddRange(overdueTask, dueTodayTask, seventhDayTask, eighthDayTask, recentCompleted, smud, ticket, meeting);
            await context.SaveChangesAsync();

            var service = new InsightService(new InsightRepository(context), new FixedTimeProvider(ReferenceNow));
            DashboardData dashboard = await service.GetDashboardAsync();

            Assert.Equal(4, dashboard.ActivePendingTasks);
            Assert.Equal(1, dashboard.ActiveSmuds);
            Assert.Equal(1, dashboard.ActiveSupportTickets);
            Assert.Equal(2, dashboard.OverdueCount);
            Assert.Equal(3, dashboard.DueSoonCount);
            Assert.Equal(1, dashboard.RecentlyCompletedCount);
            Assert.Equal(1, dashboard.DraftMeetingCount);

            DashboardItemListData overdue = await service.SearchItemsAsync(
                null,
                null,
                DashboardItemStateFilter.Overdue);
            Assert.Equal(2, overdue.Items.Count);
            Assert.All(overdue.Items, item => Assert.True(item.IsOverdue));

            DashboardItemListData dueSoon = await service.SearchItemsAsync(
                null,
                null,
                DashboardItemStateFilter.DueSoon);
            Assert.Equal(3, dueSoon.Items.Count);
            Assert.Contains(dueSoon.Items, item => item.SourceId == dueTodayTask.Id);
            Assert.Contains(dueSoon.Items, item => item.SourceId == seventhDayTask.Id);
            Assert.DoesNotContain(dueSoon.Items, item => item.SourceId == eighthDayTask.Id);

            DashboardItemListData searched = await service.SearchItemsAsync(
                "sétimo",
                TrackedEntityType.PendingTask,
                DashboardItemStateFilter.Active);
            Assert.Equal(seventhDayTask.Id, Assert.Single(searched.Items).SourceId);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GlobalHistoryFiltersInclusiveLocalDatesAndLoadsEntityTimeline()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: false);
            TeamArea area = await context.TeamAreas.FirstAsync();
            StatusDefinition status = await ActiveStatusAsync(context, WorkItemScope.PendingTask);
            PendingTask task = CreatePending("Auditar fechamento", Today, status.Id, area.Id, ReferenceNow.UtcDateTime);
            context.PendingTasks.Add(task);
            context.ActivityHistories.AddRange(
                CreateHistory(task.Id, "Evento fora do período", new DateTime(2026, 7, 12, 23, 59, 0, DateTimeKind.Utc)),
                CreateHistory(task.Id, "Evento pesquisável no fim do dia", new DateTime(2026, 7, 13, 23, 59, 0, DateTimeKind.Utc)));
            await context.SaveChangesAsync();

            var service = new InsightService(new InsightRepository(context), new FixedTimeProvider(ReferenceNow));
            GlobalHistoryPage page = await service.SearchHistoryAsync(new GlobalHistorySearch(
                Search: "pesquisável",
                EntityType: TrackedEntityType.PendingTask,
                ActionType: ActivityActionType.Updated,
                FromDate: Today,
                ToDate: Today));

            GlobalHistoryEntry entry = Assert.Single(page.Items);
            Assert.Equal("Evento pesquisável no fim do dia", entry.Summary);
            Assert.Equal(1, page.TotalCount);

            HistoryDetailsData details = Assert.IsType<HistoryDetailsData>(
                await service.GetHistoryDetailsAsync(TrackedEntityType.PendingTask, task.Id));
            Assert.Equal("Auditar fechamento", details.Subject.Title);
            Assert.Equal(2, details.Events.Count);
            Assert.True(details.Events[0].OccurredAtUtc > details.Events[1].OccurredAtUtc);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static PendingTask CreatePending(
        string title,
        DateOnly? dueDate,
        Guid statusId,
        Guid areaId,
        DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            ResponsibleAreaId = areaId,
            StatusDefinitionId = statusId,
            DueDate = dueDate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

    private static ActivityHistory CreateHistory(Guid entityId, string summary, DateTime occurredAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        EntityType = TrackedEntityType.PendingTask,
        EntityId = entityId,
        ActionType = ActivityActionType.Updated,
        OccurredAtUtc = occurredAtUtc,
        Summary = summary,
    };

    private static Task<StatusDefinition> ActiveStatusAsync(CentraSaDbContext context, WorkItemScope scope) =>
        context.StatusDefinitions.FirstAsync(item => item.Scope == scope && item.LifecycleState == LifecycleState.Active);

    private static Task<StatusDefinition> CompletedStatusAsync(CentraSaDbContext context, WorkItemScope scope) =>
        context.StatusDefinitions.SingleAsync(item => item.Scope == scope && item.LifecycleState == LifecycleState.Completed);

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CentraSA.InsightTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static DbContextOptions<CentraSaDbContext> CreateOptions(string directory) =>
        new DbContextOptionsBuilder<CentraSaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "test.db")};Foreign Keys=True")
            .Options;

    private static bool HasIndex(CentraSaDbContext context, Type entityType, params string[] properties) =>
        context.Model.FindEntityType(entityType)!.GetIndexes().Any(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(properties));

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => value;
    }
}
