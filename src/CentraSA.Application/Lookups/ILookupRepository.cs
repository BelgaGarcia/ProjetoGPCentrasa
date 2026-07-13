using CentraSA.Domain.Entities;

namespace CentraSA.Application.Lookups;

public interface ILookupRepository
{
    Task<LookupReferenceData> GetAllAsync(bool track, CancellationToken cancellationToken = default);

    Task<bool> IsStatusInUseAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> IsCategoryInUseAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(TeamArea area);

    void Add(Person person);

    void Add(StatusDefinition status);

    void Add(Category category);

    void AddHistory(ActivityHistory history);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
