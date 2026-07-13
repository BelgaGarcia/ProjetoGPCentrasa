using CentraSA.Application.Common;
using CentraSA.Application.DailyMeetings;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Repositories;

public sealed class DailyMeetingRepository(CentraSaDbContext dbContext) : IDailyMeetingRepository
{
    public async Task<IReadOnlyList<DailyMeeting>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.DailyMeetings.AsNoTracking()
            .Include(meeting => meeting.Items)
            .Where(meeting => meeting.ArchivedAtUtc == null)
            .OrderByDescending(meeting => meeting.MeetingDate)
            .ThenByDescending(meeting => meeting.StartedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<DailyMeeting?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        dbContext.DailyMeetings.AsNoTracking()
            .Include(meeting => meeting.Items)
            .Where(meeting => meeting.ArchivedAtUtc == null)
            .OrderByDescending(meeting => meeting.MeetingDate)
            .ThenByDescending(meeting => meeting.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DailyMeeting?> GetByIdAsync(
        Guid id,
        bool track,
        CancellationToken cancellationToken = default)
    {
        IQueryable<DailyMeeting> query = dbContext.DailyMeetings
            .AsSplitQuery()
            .Include(meeting => meeting.Items)
                .ThenInclude(item => item.PendingTask)
                    .ThenInclude(task => task!.StatusDefinition)
            .Include(meeting => meeting.Items)
                .ThenInclude(item => item.Smud)
                    .ThenInclude(smud => smud!.StatusDefinition)
            .Include(meeting => meeting.Items)
                .ThenInclude(item => item.SupportTicket)
                    .ThenInclude(ticket => ticket!.StatusDefinition)
            .Where(meeting => meeting.ArchivedAtUtc == null);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(meeting => meeting.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MeetingSourceCandidate>> GetSourceCandidatesAsync(
        DateTime completedSinceUtc,
        CancellationToken cancellationToken = default)
    {
        List<PendingTask> tasks = await dbContext.PendingTasks.AsNoTracking()
            .Include(task => task.ResponsibleArea)
            .Include(task => task.ResponsiblePerson)
            .Include(task => task.StatusDefinition)
            .Where(task => task.ArchivedAtUtc == null
                && (task.StatusDefinition.LifecycleState == LifecycleState.Active
                    || (task.CompletedAtUtc != null && task.CompletedAtUtc >= completedSinceUtc)))
            .ToListAsync(cancellationToken);
        List<Smud> smuds = await dbContext.Smuds.AsNoTracking()
            .Include(smud => smud.ResponsibleArea)
            .Include(smud => smud.ResponsiblePerson)
            .Include(smud => smud.StatusDefinition)
            .Where(smud => smud.ArchivedAtUtc == null
                && (smud.StatusDefinition.LifecycleState == LifecycleState.Active
                    || (smud.CompletedAtUtc != null && smud.CompletedAtUtc >= completedSinceUtc)))
            .ToListAsync(cancellationToken);
        List<SupportTicket> tickets = await dbContext.SupportTickets.AsNoTracking()
            .Include(ticket => ticket.ResponsibleArea)
            .Include(ticket => ticket.ResponsiblePerson)
            .Include(ticket => ticket.StatusDefinition)
            .Where(ticket => ticket.ArchivedAtUtc == null
                && (ticket.StatusDefinition.LifecycleState == LifecycleState.Active
                    || (ticket.CompletedAtUtc != null && ticket.CompletedAtUtc >= completedSinceUtc)))
            .ToListAsync(cancellationToken);

        return tasks.Select(task => new MeetingSourceCandidate(
                TrackedEntityType.PendingTask,
                task.Id,
                task.Title,
                task.StatusDefinition.Name,
                task.StatusDefinition.LifecycleState,
                task.DueDate,
                FormatResponsible(task.ResponsibleArea.Name, task.ResponsiblePerson?.DisplayName),
                task.CompletedAtUtc,
                task.PresentationOrder))
            .Concat(smuds.Select(smud => new MeetingSourceCandidate(
                TrackedEntityType.Smud,
                smud.Id,
                PrefixTitle(smud.Code, smud.Title),
                smud.StatusDefinition.Name,
                smud.StatusDefinition.LifecycleState,
                smud.DueDate,
                FormatResponsible(smud.ResponsibleArea.Name, smud.ResponsiblePerson?.DisplayName),
                smud.CompletedAtUtc,
                10_000)))
            .Concat(tickets.Select(ticket => new MeetingSourceCandidate(
                TrackedEntityType.SupportTicket,
                ticket.Id,
                PrefixTitle($"Chamado {ticket.TicketNumber}", ticket.Title),
                ticket.StatusDefinition.Name,
                ticket.StatusDefinition.LifecycleState,
                ticket.DueDate,
                FormatResponsible(ticket.ResponsibleArea.Name, ticket.ResponsiblePerson?.DisplayName),
                ticket.CompletedAtUtc,
                20_000)))
            .ToList();
    }

    public Task<StatusDefinition> GetCompletedStatusAsync(
        WorkItemScope scope,
        CancellationToken cancellationToken = default) =>
        dbContext.StatusDefinitions.SingleAsync(
            status => status.Scope == scope
                && status.LifecycleState == LifecycleState.Completed
                && status.IsActive,
            cancellationToken);

    public void Add(DailyMeeting meeting) => dbContext.DailyMeetings.Add(meeting);

    public void RemoveItem(DailyMeetingItem item) => dbContext.DailyMeetingItems.Remove(item);

    public void AddHistory(ActivityHistory history) => dbContext.ActivityHistories.Add(history);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
    }

    private static string FormatResponsible(string area, string? person) =>
        string.IsNullOrWhiteSpace(person) ? area : $"{area} · {person}";

    private static string PrefixTitle(string prefix, string title)
    {
        string value = $"{prefix} — {title}";
        return value.Length <= 200 ? value : value[..200];
    }
}
