using CentraSA.Application.Common;
using CentraSA.Application.SupportTickets;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Repositories;

public sealed class SupportTicketRepository(CentraSaDbContext dbContext) : ISupportTicketRepository
{
    public async Task<IReadOnlyList<SupportTicket>> SearchAsync(
        SupportTicketSearch search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SupportTicket> query = dbContext.SupportTickets
            .AsNoTracking()
            .Include(ticket => ticket.Category)
            .Include(ticket => ticket.ResponsibleArea)
            .Include(ticket => ticket.ResponsiblePerson)
            .Include(ticket => ticket.StatusDefinition);

        query = search.ArchivedOnly
            ? query.Where(ticket => ticket.ArchivedAtUtc != null)
            : query.Where(ticket => ticket.ArchivedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            string term = search.Search.Trim();
            query = query.Where(ticket =>
                ticket.NormalizedNumber.Contains(term)
                || ticket.Title.Contains(term)
                || (ticket.Description != null && ticket.Description.Contains(term))
                || (ticket.PendingAction != null && ticket.PendingAction.Contains(term)));
        }

        if (search.CategoryId.HasValue)
        {
            query = query.Where(ticket => ticket.CategoryId == search.CategoryId.Value);
        }

        if (search.AreaId.HasValue)
        {
            query = query.Where(ticket => ticket.ResponsibleAreaId == search.AreaId.Value);
        }

        if (search.PersonId.HasValue)
        {
            query = query.Where(ticket => ticket.ResponsiblePersonId == search.PersonId.Value);
        }

        if (search.StatusId.HasValue)
        {
            query = query.Where(ticket => ticket.StatusDefinitionId == search.StatusId.Value);
        }

        if (search.HideFinalized)
        {
            query = query.Where(ticket => ticket.StatusDefinition.LifecycleState == LifecycleState.Active);
        }

        if (search.ActionRequiredOnly)
        {
            query = query.Where(ticket =>
                ticket.PendingAction != null
                && ticket.PendingAction != string.Empty
                && ticket.StatusDefinition.LifecycleState == LifecycleState.Active);
        }

        query = search.DueFilter switch
        {
            SupportTicketDueFilter.Overdue => query.Where(ticket =>
                ticket.DueDate != null
                && ticket.DueDate < search.Today
                && ticket.StatusDefinition.LifecycleState == LifecycleState.Active),
            SupportTicketDueFilter.DueSoon => query.Where(ticket =>
                ticket.DueDate != null
                && ticket.DueDate >= search.Today
                && ticket.DueDate <= search.Today.AddDays(7)
                && ticket.StatusDefinition.LifecycleState == LifecycleState.Active),
            SupportTicketDueFilter.NoDueDate => query.Where(ticket => ticket.DueDate == null),
            _ => query,
        };

        return await query
            .OrderBy(ticket => ticket.Category.SortOrder)
            .ThenBy(ticket => ticket.DueDate.HasValue ? 0 : 1)
            .ThenBy(ticket => ticket.DueDate)
            .ThenBy(ticket => ticket.NormalizedNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<SupportTicket?> GetByIdAsync(
        Guid id,
        bool includeArchived,
        bool track,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SupportTicket> query = dbContext.SupportTickets
            .Include(ticket => ticket.Category)
            .Include(ticket => ticket.ResponsibleArea)
            .Include(ticket => ticket.ResponsiblePerson)
            .Include(ticket => ticket.StatusDefinition);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        if (!includeArchived)
        {
            query = query.Where(ticket => ticket.ArchivedAtUtc == null);
        }

        return await query.SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
    }

    public async Task<SupportTicketReferenceData> GetReferenceDataAsync(
        CancellationToken cancellationToken = default)
    {
        List<TeamArea> areas = await dbContext.TeamAreas.AsNoTracking()
            .Where(area => area.IsActive)
            .OrderBy(area => area.Name)
            .ToListAsync(cancellationToken);
        List<Person> people = await dbContext.People.AsNoTracking()
            .Include(person => person.TeamArea)
            .Where(person => person.IsActive)
            .OrderBy(person => person.DisplayName)
            .ToListAsync(cancellationToken);
        List<StatusDefinition> statuses = await dbContext.StatusDefinitions.AsNoTracking()
            .Where(status => status.Scope == WorkItemScope.SupportTicket && status.IsActive)
            .OrderBy(status => status.SortOrder)
            .ToListAsync(cancellationToken);
        List<Category> categories = await dbContext.Categories.AsNoTracking()
            .Where(category => category.Scope == WorkItemScope.SupportTicket && category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ToListAsync(cancellationToken);

        return new SupportTicketReferenceData(areas, people, statuses, categories);
    }

    public async Task<IReadOnlyList<ActivityHistory>> GetHistoryAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ActivityHistories.AsNoTracking()
            .Where(history => history.EntityType == TrackedEntityType.SupportTicket && history.EntityId == ticketId)
            .OrderByDescending(history => history.OccurredAtUtc)
            .ToListAsync(cancellationToken);

    public Task<bool> IsNumberInUseAsync(
        string normalizedNumber,
        Guid? excludingId,
        CancellationToken cancellationToken = default) =>
        dbContext.SupportTickets.AnyAsync(
            ticket => ticket.NormalizedNumber == normalizedNumber
                && (!excludingId.HasValue || ticket.Id != excludingId.Value),
            cancellationToken);

    public void Add(SupportTicket ticket) => dbContext.SupportTickets.Add(ticket);

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
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException
            {
                SqliteErrorCode: 19,
                SqliteExtendedErrorCode: 2067,
            })
        {
            throw new DuplicateSupportTicketNumberException(exception);
        }
    }
}
