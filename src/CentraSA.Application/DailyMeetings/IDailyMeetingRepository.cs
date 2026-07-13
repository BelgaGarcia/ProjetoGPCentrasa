using CentraSA.Domain.Entities;
using CentraSA.Domain.Enums;

namespace CentraSA.Application.DailyMeetings;

public interface IDailyMeetingRepository
{
    Task<IReadOnlyList<DailyMeeting>> ListAsync(CancellationToken cancellationToken = default);

    Task<DailyMeeting?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<DailyMeeting?> GetByIdAsync(
        Guid id,
        bool track,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeetingSourceCandidate>> GetSourceCandidatesAsync(
        DateTime completedSinceUtc,
        CancellationToken cancellationToken = default);

    Task<StatusDefinition> GetCompletedStatusAsync(
        WorkItemScope scope,
        CancellationToken cancellationToken = default);

    void Add(DailyMeeting meeting);

    void RemoveItem(DailyMeetingItem item);

    void AddHistory(ActivityHistory history);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
