using CentraSA.Application.Common;
using CentraSA.Application.Smuds;
using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;
using CentraSA.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Repositories;

public sealed class SmudRepository(CentraSaDbContext dbContext) : ISmudRepository
{
    public async Task<IReadOnlyList<Smud>> SearchAsync(
        SmudSearch search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Smud> query = dbContext.Smuds
            .AsNoTracking()
            .Include(smud => smud.ResponsibleArea)
            .Include(smud => smud.ResponsiblePerson)
            .Include(smud => smud.StatusDefinition);

        query = search.ArchivedOnly
            ? query.Where(smud => smud.ArchivedAtUtc != null)
            : query.Where(smud => smud.ArchivedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            string term = search.Search.Trim();
            string normalizedTerm = term.ToUpperInvariant()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);
            query = query.Where(smud =>
                smud.NormalizedCode.Contains(normalizedTerm)
                || smud.Title.Contains(term)
                || (smud.Description != null && smud.Description.Contains(term))
                || (smud.RequiredAction != null && smud.RequiredAction.Contains(term)));
        }

        if (search.AreaId.HasValue)
        {
            query = query.Where(smud => smud.ResponsibleAreaId == search.AreaId.Value);
        }

        if (search.PersonId.HasValue)
        {
            query = query.Where(smud => smud.ResponsiblePersonId == search.PersonId.Value);
        }

        if (search.StatusId.HasValue)
        {
            query = query.Where(smud => smud.StatusDefinitionId == search.StatusId.Value);
        }

        if (search.HideFinalized)
        {
            query = query.Where(smud => smud.StatusDefinition.LifecycleState == LifecycleState.Active);
        }

        if (search.ActionRequiredOnly)
        {
            query = query.Where(smud =>
                smud.RequiredAction != null
                && smud.RequiredAction != string.Empty
                && smud.StatusDefinition.LifecycleState == LifecycleState.Active);
        }

        query = search.DueFilter switch
        {
            SmudDueFilter.Overdue => query.Where(smud =>
                smud.DueDate != null
                && smud.DueDate < search.Today
                && smud.StatusDefinition.LifecycleState == LifecycleState.Active),
            SmudDueFilter.DueSoon => query.Where(smud =>
                smud.DueDate != null
                && smud.DueDate >= search.Today
                && smud.DueDate <= search.Today.AddDays(7)
                && smud.StatusDefinition.LifecycleState == LifecycleState.Active),
            SmudDueFilter.NoDueDate => query.Where(smud => smud.DueDate == null),
            _ => query,
        };

        return await query
            .OrderBy(smud => smud.StatusDefinition.SortOrder)
            .ThenBy(smud => smud.DueDate.HasValue ? 0 : 1)
            .ThenBy(smud => smud.DueDate)
            .ThenBy(smud => smud.NormalizedCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<Smud?> GetByIdAsync(
        Guid id,
        bool includeArchived,
        bool track,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Smud> query = dbContext.Smuds
            .Include(smud => smud.ResponsibleArea)
            .Include(smud => smud.ResponsiblePerson)
            .Include(smud => smud.StatusDefinition);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        if (!includeArchived)
        {
            query = query.Where(smud => smud.ArchivedAtUtc == null);
        }

        return await query.SingleOrDefaultAsync(smud => smud.Id == id, cancellationToken);
    }

    public async Task<SmudReferenceData> GetReferenceDataAsync(CancellationToken cancellationToken = default)
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
            .Where(status => status.Scope == WorkItemScope.Smud && status.IsActive)
            .OrderBy(status => status.SortOrder)
            .ToListAsync(cancellationToken);

        return new SmudReferenceData(areas, people, statuses);
    }

    public async Task<IReadOnlyList<ActivityHistory>> GetHistoryAsync(
        Guid smudId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ActivityHistories.AsNoTracking()
            .Where(history => history.EntityType == TrackedEntityType.Smud && history.EntityId == smudId)
            .OrderByDescending(history => history.OccurredAtUtc)
            .ToListAsync(cancellationToken);

    public Task<bool> IsCodeInUseAsync(
        string normalizedCode,
        Guid? excludingId,
        CancellationToken cancellationToken = default) =>
        dbContext.Smuds.AnyAsync(
            smud => smud.NormalizedCode == normalizedCode
                && (!excludingId.HasValue || smud.Id != excludingId.Value),
            cancellationToken);

    public void Add(Smud smud) => dbContext.Smuds.Add(smud);

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
            throw new DuplicateSmudCodeException(exception);
        }
    }
}
