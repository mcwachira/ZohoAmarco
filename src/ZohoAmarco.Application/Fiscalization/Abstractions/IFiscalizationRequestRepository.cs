using ZohoAmarco.Domain.Fiscalization;

namespace ZohoAmarco.Application.Fiscalization.Abstractions;

///<summary>
/// Defines durable storage operation for fiscalization requests.
/// Implentation must enforce idempotency and processing claims
/// atomically in the database
/// </summary>

public interface IFiscalizationRequestRepository
{
    
    ///<summary>
    /// Atomically creates a request or returns the existing request
    /// with the same idempotency key
    /// </summary>
    
    Task<FiscalizationRequest> GetOrCreateAsync(FiscalizationRequest candidate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Atomically changes Pending to InProgress.
    /// Returns null if another worker already claimed the request
    /// or the request is no longer Pending.
    /// The returned entity must reflect the persisted InProgress state.
    /// </summary>
    Task<FiscalizationRequest?> TryStartProcessingAsync(
        Guid requestId,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken = default);

    Task<FiscalizationRequest?> GetByIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        FiscalizationRequest request,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<FiscalizationRequest>> GetStaleInProgressAsync(
        DateTimeOffset startedBeforeUtc,
        CancellationToken cancellationToken = default);
}
