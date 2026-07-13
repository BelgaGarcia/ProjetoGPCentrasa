using CentraSA.Domain.Entities;

namespace CentraSA.Application.Smuds;

public interface ISmudRepository
{
    Task<IReadOnlyList<Smud>> SearchAsync(
        SmudSearch search,
        CancellationToken cancellationToken = default);

    Task<Smud?> GetByIdAsync(
        Guid id,
        bool includeArchived,
        bool track,
        CancellationToken cancellationToken = default);

    Task<SmudReferenceData> GetReferenceDataAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityHistory>> GetHistoryAsync(
        Guid smudId,
        CancellationToken cancellationToken = default);

    Task<bool> IsCodeInUseAsync(
        string normalizedCode,
        Guid? excludingId,
        CancellationToken cancellationToken = default);

    void Add(Smud smud);

    void AddHistory(ActivityHistory history);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
