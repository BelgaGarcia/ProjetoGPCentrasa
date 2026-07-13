using CentraSA.Application.DailyMeetings;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using CentraSA.Infrastructure.Repositories;
using CentraSA.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.IntegrationTests;

public sealed class DailyMeetingWorkflowTests
{
    private static readonly DateTimeOffset ReferenceNow = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BuilderSuggestsPrioritySectionsAndRejectsTheSameSourceTwice()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: true);
            var service = CreateService(context);
            DailyMeetingBuilderData builder = await service.GetCreateBuilderAsync();

            Assert.Contains(builder.Rows, row => row.RecommendedSection == MeetingSection.Overdue && row.Selected);
            Assert.Contains(builder.Rows, row => row.RecommendedSection == MeetingSection.DueSoon && row.Selected);
            Assert.Equal(
                builder.Rows.Count,
                builder.Rows.Select(row => (row.SourceType, row.SourceId)).Distinct().Count());

            DailyMeetingBuilderRow source = builder.Rows[0];
            DailyMeetingOperationResult duplicate = await service.CreateDraftAsync(
                new DailyMeetingInput
                {
                    MeetingDate = builder.MeetingDate,
                    Items =
                    [
                        CreateSelection(source, MeetingSection.Overdue, 10),
                        CreateSelection(source, MeetingSection.PendingTasks, 20),
                    ],
                },
                Guid.NewGuid());

            Assert.Equal(DailyMeetingOperationStatus.ValidationFailed, duplicate.Status);
            Assert.Contains(duplicate.Errors!, error => error.Contains("apenas uma vez", StringComparison.Ordinal));
            Assert.Empty(await context.DailyMeetings.AsNoTracking().ToListAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SessionPreservesSnapshotsUpdatesOriginalAndBecomesReadOnlyAfterFinish()
    {
        string directory = CreateTemporaryDirectory();
        DbContextOptions<CentraSaDbContext> options = CreateOptions(directory);

        try
        {
            await using var context = new CentraSaDbContext(options);
            await new DatabaseInitializer(context).InitializeAsync(seedDemoData: true);
            var service = CreateService(context);
            Guid actorId = Guid.NewGuid();
            DailyMeetingBuilderData builder = await service.GetCreateBuilderAsync();
            DailyMeetingBuilderRow pendingTask = builder.Rows.First(row => row.SourceType == TrackedEntityType.PendingTask);
            DailyMeetingBuilderRow smud = builder.Rows.First(row => row.SourceType == TrackedEntityType.Smud);
            DailyMeetingBuilderRow ticket = builder.Rows.First(row => row.SourceType == TrackedEntityType.SupportTicket);

            DailyMeetingOperationResult created = await service.CreateDraftAsync(
                new DailyMeetingInput
                {
                    MeetingDate = builder.MeetingDate,
                    GeneralNotes = "  Prioridades da operação  ",
                    Items =
                    [
                        CreateSelection(pendingTask, MeetingSection.PendingTasks, 30),
                        CreateSelection(smud, MeetingSection.Smuds, 10),
                        CreateSelection(ticket, MeetingSection.SupportTickets, 20),
                    ],
                },
                actorId);

            Assert.True(created.Succeeded);
            Guid meetingId = Assert.IsType<Guid>(created.Id);
            DailyMeetingDetailsData details = Assert.IsType<DailyMeetingDetailsData>(
                await service.GetDetailsAsync(meetingId));
            Assert.Equal("Prioridades da operação", details.GeneralNotes);
            Assert.Equal(3, details.Items.Count);
            Assert.Equal([10, 20, 30], details.Items.Select(item => item.SortOrder).Order().ToArray());
            Assert.Equal(
                details.Items.Count,
                details.Items.Select(item => (item.SourceType, item.SourceId)).Distinct().Count());

            DailyMeetingItemData taskItem = details.Items.Single(item => item.SourceType == TrackedEntityType.PendingTask);
            string snapshotTitle = taskItem.SnapshotTitle;
            string snapshotStatus = taskItem.SnapshotStatus;
            DateOnly? snapshotDueDate = taskItem.SnapshotDueDate;
            string? snapshotResponsible = taskItem.SnapshotResponsible;

            DailyMeetingOperationResult notesResult = await service.UpdateItemNotesAsync(
                meetingId,
                taskItem.Id,
                details.Version,
                "  Cobrar retorno até o fim do dia  ",
                actorId);
            Assert.True(notesResult.Succeeded);

            details = Assert.IsType<DailyMeetingDetailsData>(await service.GetDetailsAsync(meetingId));
            taskItem = details.Items.Single(item => item.Id == taskItem.Id);
            Assert.Equal("Cobrar retorno até o fim do dia", taskItem.PresentationNotes);

            DailyMeetingOperationResult completed = await service.CompleteOriginalAsync(
                meetingId,
                taskItem.Id,
                details.Version,
                actorId);
            Assert.True(completed.Succeeded);

            details = Assert.IsType<DailyMeetingDetailsData>(await service.GetDetailsAsync(meetingId));
            taskItem = details.Items.Single(item => item.Id == taskItem.Id);
            Assert.True(taskItem.WasPresented);
            Assert.True(taskItem.OriginalIsCompleted);
            Assert.Equal(snapshotTitle, taskItem.SnapshotTitle);
            Assert.Equal(snapshotStatus, taskItem.SnapshotStatus);
            Assert.Equal(snapshotDueDate, taskItem.SnapshotDueDate);
            Assert.Equal(snapshotResponsible, taskItem.SnapshotResponsible);
            Assert.NotEqual(taskItem.SnapshotStatus, taskItem.CurrentStatus);

            PendingTask original = await context.PendingTasks.AsNoTracking()
                .Include(task => task.StatusDefinition)
                .SingleAsync(task => task.Id == taskItem.SourceId);
            Assert.Equal(LifecycleState.Completed, original.StatusDefinition.LifecycleState);
            Assert.NotNull(original.CompletedAtUtc);
            Assert.Contains(
                await context.ActivityHistories.AsNoTracking().Where(history => history.EntityId == original.Id).ToListAsync(),
                history => history.ActionType == ActivityActionType.Completed);

            DailyMeetingOverviewData overview = await service.GetOverviewAsync();
            Assert.Equal(meetingId, overview.LatestMeeting?.Id);
            Assert.Equal(1, overview.LatestMeeting?.PresentedCount);

            DailyMeetingOperationResult finished = await service.FinishAsync(
                meetingId,
                details.Version,
                actorId);
            Assert.True(finished.Succeeded);

            details = Assert.IsType<DailyMeetingDetailsData>(await service.GetDetailsAsync(meetingId));
            Assert.Equal(MeetingStatus.Finished, details.Status);
            Assert.NotNull(details.FinishedAtUtc);

            DailyMeetingOperationResult afterFinish = await service.MarkPresentedAsync(
                meetingId,
                details.Items[1].Id,
                details.Version,
                wasPresented: true,
                actorId);
            Assert.Equal(DailyMeetingOperationStatus.AlreadyFinished, afterFinish.Status);
            Assert.Null(await service.GetEditBuilderAsync(meetingId));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PresentationLayoutUsesResponsiveGridWithoutAbsoluteCoordinates()
    {
        string repositoryRoot = FindRepositoryRoot();
        string css = File.ReadAllText(Path.Combine(repositoryRoot, "src", "CentraSA.Web", "wwwroot", "css", "pages.css"));
        int rulesStart = css.IndexOf(".meeting-latest", StringComparison.Ordinal);
        int responsiveStart = css.IndexOf("@media (max-width: 1199.98px)", rulesStart, StringComparison.Ordinal);
        string meetingRules = css[rulesStart..responsiveStart];

        Assert.Contains(".meeting-presentation-card", meetingRules, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns", meetingRules, StringComparison.Ordinal);
        Assert.Contains(".meeting-presentation-card__relation i", meetingRules, StringComparison.Ordinal);
        Assert.DoesNotContain("position: absolute", meetingRules, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 767.98px)", css[responsiveStart..], StringComparison.Ordinal);
    }

    private static DailyMeetingService CreateService(CentraSaDbContext context) =>
        new(new DailyMeetingRepository(context), new FixedTimeProvider(ReferenceNow));

    private static DailyMeetingSelectionInput CreateSelection(
        DailyMeetingBuilderRow row,
        MeetingSection section,
        int sortOrder) => new()
        {
            SourceType = row.SourceType,
            SourceId = row.SourceId,
            Selected = true,
            Section = section,
            SortOrder = sortOrder,
        };

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CentraSA.DailyMeetingTests", Guid.NewGuid().ToString("N"));
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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => value;
    }
}
