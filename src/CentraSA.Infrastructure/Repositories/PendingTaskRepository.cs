using CentraSA.Application.Common;
using CentraSA.Application.PendingTasks;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Repositories;

public sealed class PendingTaskRepository(CentraSaDbContext dbContext) : IPendingTaskRepository
{
    public async Task<PendingTaskPage> SearchAsync(
        PendingTaskSearch search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PendingTask> query = dbContext.PendingTasks
            .AsNoTracking()
            .AsSplitQuery()
            .Include(task => task.ResponsibleArea)
            .Include(task => task.ResponsiblePerson)
            .Include(task => task.StatusDefinition)
            .Include(task => task.Category)
            .Include(task => task.References)
                .ThenInclude(reference => reference.Smud)
            .Include(task => task.References)
                .ThenInclude(reference => reference.SupportTicket);

        query = search.ArchivedOnly
            ? query.Where(task => task.ArchivedAtUtc != null)
            : query.Where(task => task.ArchivedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            string term = search.Search.Trim();
            query = query.Where(task =>
                task.Title.Contains(term)
                || (task.Description != null && task.Description.Contains(term))
                || (task.Origin != null && task.Origin.Contains(term)));
        }

        if (search.AreaId.HasValue)
        {
            query = query.Where(task => task.ResponsibleAreaId == search.AreaId.Value);
        }

        if (search.PersonId.HasValue)
        {
            query = query.Where(task => task.ResponsiblePersonId == search.PersonId.Value);
        }

        if (search.StatusId.HasValue)
        {
            query = query.Where(task => task.StatusDefinitionId == search.StatusId.Value);
        }

        if (search.HideCompleted)
        {
            query = query.Where(task => task.StatusDefinition.LifecycleState != LifecycleState.Completed);
        }

        query = search.DueFilter switch
        {
            PendingTaskDueFilter.Overdue => query.Where(task =>
                task.DueDate != null
                && task.DueDate < search.Today
                && task.StatusDefinition.LifecycleState == LifecycleState.Active),
            PendingTaskDueFilter.DueSoon => query.Where(task =>
                task.DueDate != null
                && task.DueDate >= search.Today
                && task.DueDate <= search.Today.AddDays(7)
                && task.StatusDefinition.LifecycleState == LifecycleState.Active),
            PendingTaskDueFilter.NoDueDate => query.Where(task => task.DueDate == null),
            _ => query,
        };

        int totalCount = await query.CountAsync(cancellationToken);
        int page = Math.Max(1, search.Page);
        int pageSize = Math.Clamp(search.PageSize, 1, 5000);
        List<PendingTask> items = await query
            .OrderBy(task => task.PresentationOrder)
            .ThenBy(task => task.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PendingTaskPage(items, totalCount, page, pageSize);
    }

    public async Task<PendingTask?> GetByIdAsync(
        Guid id,
        bool includeArchived,
        bool track,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PendingTask> query = dbContext.PendingTasks
            .AsSplitQuery()
            .Include(task => task.ResponsibleArea)
            .Include(task => task.ResponsiblePerson)
            .Include(task => task.StatusDefinition)
            .Include(task => task.Category)
            .Include(task => task.References)
                .ThenInclude(reference => reference.Smud)
            .Include(task => task.References)
                .ThenInclude(reference => reference.SupportTicket);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        if (!includeArchived)
        {
            query = query.Where(task => task.ArchivedAtUtc == null);
        }

        return await query.SingleOrDefaultAsync(task => task.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingTask>> GetOrderedActiveAsync(
        CancellationToken cancellationToken = default) =>
        await dbContext.PendingTasks
            .Where(task => task.ArchivedAtUtc == null)
            .OrderBy(task => task.PresentationOrder)
            .ThenBy(task => task.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<PendingTaskReferenceData> GetReferenceDataAsync(
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
            .Where(status => status.Scope == WorkItemScope.PendingTask && status.IsActive)
            .OrderBy(status => status.SortOrder)
            .ToListAsync(cancellationToken);
        List<Category> categories = await dbContext.Categories.AsNoTracking()
            .Where(category => category.Scope == WorkItemScope.PendingTask && category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ToListAsync(cancellationToken);
        List<Smud> smuds = await dbContext.Smuds.AsNoTracking()
            .Where(smud => smud.ArchivedAtUtc == null)
            .OrderBy(smud => smud.NormalizedCode)
            .ToListAsync(cancellationToken);
        List<SupportTicket> tickets = await dbContext.SupportTickets.AsNoTracking()
            .Where(ticket => ticket.ArchivedAtUtc == null)
            .OrderBy(ticket => ticket.NormalizedNumber)
            .ToListAsync(cancellationToken);

        return new PendingTaskReferenceData(areas, people, statuses, categories, smuds, tickets);
    }

    public async Task<IReadOnlyList<ActivityHistory>> GetHistoryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ActivityHistories.AsNoTracking()
            .Where(history => history.EntityType == TrackedEntityType.PendingTask && history.EntityId == taskId)
            .OrderByDescending(history => history.OccurredAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextPresentationOrderAsync(CancellationToken cancellationToken = default)
    {
        int? maximum = await dbContext.PendingTasks
            .Where(task => task.ArchivedAtUtc == null)
            .MaxAsync(task => (int?)task.PresentationOrder, cancellationToken);
        return (maximum ?? 0) + 10;
    }

    public void Add(PendingTask task) => dbContext.PendingTasks.Add(task);

    public void AddHistory(ActivityHistory history) => dbContext.ActivityHistories.Add(history);

    public async Task ReplaceReferenceAsync(
        Guid taskId,
        Guid? smudId,
        Guid? supportTicketId,
        CancellationToken cancellationToken = default)
    {
        List<WorkItemReference> existing = await dbContext.WorkItemReferences
            .Where(reference => reference.PendingTaskId == taskId)
            .ToListAsync(cancellationToken);
        dbContext.WorkItemReferences.RemoveRange(existing);

        if (smudId.HasValue || supportTicketId.HasValue)
        {
            dbContext.WorkItemReferences.Add(new WorkItemReference
            {
                Id = Guid.NewGuid(),
                PendingTaskId = taskId,
                SmudId = smudId,
                SupportTicketId = supportTicketId,
            });
        }
    }

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
}
