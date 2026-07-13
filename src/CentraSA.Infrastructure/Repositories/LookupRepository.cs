using CentraSA.Application.Common;
using CentraSA.Application.Lookups;
using CentraSA.Domain.Entities;
using CentraSA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentraSA.Infrastructure.Repositories;

public sealed class LookupRepository(CentraSaDbContext dbContext) : ILookupRepository
{
    public async Task<LookupReferenceData> GetAllAsync(
        bool track,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TeamArea> areas = dbContext.TeamAreas;
        IQueryable<Person> people = dbContext.People.Include(person => person.TeamArea);
        IQueryable<StatusDefinition> statuses = dbContext.StatusDefinitions;
        IQueryable<Category> categories = dbContext.Categories;
        if (!track)
        {
            areas = areas.AsNoTracking();
            people = people.AsNoTracking();
            statuses = statuses.AsNoTracking();
            categories = categories.AsNoTracking();
        }

        return new LookupReferenceData(
            await areas.OrderBy(area => area.Name).ToListAsync(cancellationToken),
            await people.OrderBy(person => person.DisplayName).ToListAsync(cancellationToken),
            await statuses.OrderBy(status => status.Scope).ThenBy(status => status.SortOrder).ToListAsync(cancellationToken),
            await categories.OrderBy(category => category.Scope).ThenBy(category => category.SortOrder).ToListAsync(cancellationToken));
    }

    public async Task<bool> IsStatusInUseAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.PendingTasks.AnyAsync(item => item.StatusDefinitionId == id, cancellationToken)
        || await dbContext.Smuds.AnyAsync(item => item.StatusDefinitionId == id, cancellationToken)
        || await dbContext.SupportTickets.AnyAsync(item => item.StatusDefinitionId == id, cancellationToken);

    public async Task<bool> IsCategoryInUseAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.PendingTasks.AnyAsync(item => item.CategoryId == id, cancellationToken)
        || await dbContext.SupportTickets.AnyAsync(item => item.CategoryId == id, cancellationToken);

    public void Add(TeamArea area) => dbContext.TeamAreas.Add(area);

    public void Add(Person person) => dbContext.People.Add(person);

    public void Add(StatusDefinition status) => dbContext.StatusDefinitions.Add(status);

    public void Add(Category category) => dbContext.Categories.Add(category);

    public void AddHistory(ActivityHistory history) => dbContext.ActivityHistories.Add(history);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new LookupConflictException(exception);
        }
    }
}
