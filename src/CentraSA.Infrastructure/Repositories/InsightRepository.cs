using CentraSA.Application.Insights;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Repositories;

public sealed class InsightRepository(CentraSaDbContext dbContext) : IInsightRepository
{
    public async Task<IReadOnlyList<OperationalItemProjection>> GetOperationalItemsAsync(
        DateTime completedSinceUtc,
        CancellationToken cancellationToken = default)
    {
        List<OperationalItemProjection> pendingTasks = await dbContext.PendingTasks.AsNoTracking()
            .Where(task => task.ArchivedAtUtc == null
                && (task.StatusDefinition.LifecycleState == LifecycleState.Active
                    || (task.CompletedAtUtc != null && task.CompletedAtUtc >= completedSinceUtc)))
            .Select(task => new OperationalItemProjection(
                TrackedEntityType.PendingTask,
                task.Id,
                "Pendência",
                task.Title,
                task.StatusDefinition.Name,
                task.StatusDefinition.LifecycleState,
                task.DueDate,
                task.CompletedAtUtc,
                task.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        List<OperationalItemProjection> smuds = await dbContext.Smuds.AsNoTracking()
            .Where(smud => smud.ArchivedAtUtc == null
                && (smud.StatusDefinition.LifecycleState == LifecycleState.Active
                    || (smud.CompletedAtUtc != null && smud.CompletedAtUtc >= completedSinceUtc)))
            .Select(smud => new OperationalItemProjection(
                TrackedEntityType.Smud,
                smud.Id,
                smud.Code,
                smud.Title,
                smud.StatusDefinition.Name,
                smud.StatusDefinition.LifecycleState,
                smud.DueDate,
                smud.CompletedAtUtc,
                smud.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        List<OperationalItemProjection> tickets = await dbContext.SupportTickets.AsNoTracking()
            .Where(ticket => ticket.ArchivedAtUtc == null
                && (ticket.StatusDefinition.LifecycleState == LifecycleState.Active
                    || (ticket.CompletedAtUtc != null && ticket.CompletedAtUtc >= completedSinceUtc)))
            .Select(ticket => new OperationalItemProjection(
                TrackedEntityType.SupportTicket,
                ticket.Id,
                ticket.TicketNumber,
                ticket.Title,
                ticket.StatusDefinition.Name,
                ticket.StatusDefinition.LifecycleState,
                ticket.DueDate,
                ticket.CompletedAtUtc,
                ticket.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return pendingTasks.Concat(smuds).Concat(tickets).ToList();
    }

    public Task<int> CountDraftMeetingsAsync(CancellationToken cancellationToken = default) =>
        dbContext.DailyMeetings.AsNoTracking().CountAsync(
            meeting => meeting.ArchivedAtUtc == null && meeting.Status == MeetingStatus.Draft,
            cancellationToken);

    public async Task<HistoryRepositoryPage> SearchHistoryAsync(
        HistoryRepositorySearch search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ActivityHistory> query = dbContext.ActivityHistories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            string pattern = $"%{search.Search.Trim()}%";
            query = query.Where(history => EF.Functions.Like(history.Summary, pattern));
        }

        if (search.EntityType.HasValue)
        {
            query = query.Where(history => history.EntityType == search.EntityType);
        }

        if (search.ActionType.HasValue)
        {
            query = query.Where(history => history.ActionType == search.ActionType);
        }

        if (search.FromUtc.HasValue)
        {
            query = query.Where(history => history.OccurredAtUtc >= search.FromUtc);
        }

        if (search.ToExclusiveUtc.HasValue)
        {
            query = query.Where(history => history.OccurredAtUtc < search.ToExclusiveUtc);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<HistoryRepositoryEntry> items = await query
            .OrderByDescending(history => history.OccurredAtUtc)
            .ThenByDescending(history => history.Id)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(history => new HistoryRepositoryEntry(
                history.Id,
                history.EntityType,
                history.EntityId,
                history.ActionType,
                history.Summary,
                history.OccurredAtUtc))
            .ToListAsync(cancellationToken);
        return new HistoryRepositoryPage(items, totalCount);
    }

    public async Task<IReadOnlyList<HistoryRepositoryEntry>> GetEntityHistoryAsync(
        TrackedEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ActivityHistories.AsNoTracking()
            .Where(history => history.EntityType == entityType && history.EntityId == entityId)
            .OrderByDescending(history => history.OccurredAtUtc)
            .ThenByDescending(history => history.Id)
            .Select(history => new HistoryRepositoryEntry(
                history.Id,
                history.EntityType,
                history.EntityId,
                history.ActionType,
                history.Summary,
                history.OccurredAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<HistorySubject?> GetHistorySubjectAsync(
        TrackedEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        switch (entityType)
        {
            case TrackedEntityType.PendingTask:
                return await dbContext.PendingTasks.AsNoTracking()
                    .Where(item => item.Id == entityId)
                    .Select(item => new HistorySubject(entityType, entityId, item.Title, item.StatusDefinition.Name))
                    .SingleOrDefaultAsync(cancellationToken);
            case TrackedEntityType.Smud:
                return await dbContext.Smuds.AsNoTracking()
                    .Where(item => item.Id == entityId)
                    .Select(item => new HistorySubject(entityType, entityId, item.Code + " — " + item.Title, item.StatusDefinition.Name))
                    .SingleOrDefaultAsync(cancellationToken);
            case TrackedEntityType.SupportTicket:
                return await dbContext.SupportTickets.AsNoTracking()
                    .Where(item => item.Id == entityId)
                    .Select(item => new HistorySubject(entityType, entityId, "Chamado " + item.TicketNumber + " — " + item.Title, item.StatusDefinition.Name))
                    .SingleOrDefaultAsync(cancellationToken);
            case TrackedEntityType.DailyMeeting:
                DailyMeeting? meeting = await dbContext.DailyMeetings.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == entityId, cancellationToken);
                return meeting is null
                    ? null
                    : new HistorySubject(entityType, entityId, $"Reunião {meeting.MeetingDate:dd/MM/yyyy}", MeetingStatusLabel(meeting.Status));
            case TrackedEntityType.TeamArea:
                TeamArea? area = await dbContext.TeamAreas.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == entityId, cancellationToken);
                return area is null ? null : new HistorySubject(entityType, entityId, area.Name, AreaKindLabel(area.Kind));
            case TrackedEntityType.Person:
                Person? person = await dbContext.People.AsNoTracking()
                    .Include(item => item.TeamArea)
                    .SingleOrDefaultAsync(item => item.Id == entityId, cancellationToken);
                return person is null
                    ? null
                    : new HistorySubject(entityType, entityId, person.DisplayName, person.TeamArea?.Name);
            case TrackedEntityType.StatusDefinition:
                StatusDefinition? status = await dbContext.StatusDefinitions.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == entityId, cancellationToken);
                return status is null
                    ? null
                    : new HistorySubject(entityType, entityId, status.Name, ScopeLabel(status.Scope));
            case TrackedEntityType.Category:
                Category? category = await dbContext.Categories.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == entityId, cancellationToken);
                return category is null
                    ? null
                    : new HistorySubject(entityType, entityId, category.Name, ScopeLabel(category.Scope));
            default:
                bool historyExists = await dbContext.ActivityHistories.AsNoTracking().AnyAsync(
                    history => history.EntityType == entityType && history.EntityId == entityId,
                    cancellationToken);
                return historyExists ? new HistorySubject(entityType, entityId, "Registro histórico", null) : null;
        }
    }

    private static string MeetingStatusLabel(MeetingStatus status) =>
        status == MeetingStatus.Finished ? "Finalizada" : "Rascunho";

    private static string AreaKindLabel(TeamAreaKind kind) =>
        kind == TeamAreaKind.ExternalTeam ? "Equipe externa" : "Área interna";

    private static string ScopeLabel(WorkItemScope scope) => scope switch
    {
        WorkItemScope.PendingTask => "Pendências",
        WorkItemScope.Smud => "SMUDs",
        WorkItemScope.SupportTicket => "Chamados",
        _ => scope.ToString(),
    };
}
