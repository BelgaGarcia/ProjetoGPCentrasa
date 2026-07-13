namespace CentraSA.Application.DailyMeetings;

public interface IDailyMeetingService
{
    Task<DailyMeetingOverviewData> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<DailyMeetingBuilderData> GetCreateBuilderAsync(CancellationToken cancellationToken = default);

    Task<DailyMeetingBuilderData?> GetEditBuilderAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DailyMeetingDetailsData?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DailyMeetingOperationResult> CreateDraftAsync(
        DailyMeetingInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<DailyMeetingOperationResult> UpdateDraftAsync(
        Guid id,
        DailyMeetingInput input,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<DailyMeetingOperationResult> MarkPresentedAsync(
        Guid id,
        Guid itemId,
        long version,
        bool wasPresented,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<DailyMeetingOperationResult> UpdateItemNotesAsync(
        Guid id,
        Guid itemId,
        long version,
        string? notes,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<DailyMeetingOperationResult> CompleteOriginalAsync(
        Guid id,
        Guid itemId,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<DailyMeetingOperationResult> FinishAsync(
        Guid id,
        long version,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
