namespace ZohoAmarco.Domain.Fiscalization;

/// <summary>
/// Represents the lifecycle of a fiscalization request.
/// </summary>
public enum FiscalizationStatus
{
    /// <summary>
    /// The request has been accepted but not yet processed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Processing has started.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// The fiscal device confirmed successful processing.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// The operation definitively failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The device may have processed the command,
    /// but the outcome has not been confirmed.
    /// </summary>
    OutcomeUnknown = 4
}