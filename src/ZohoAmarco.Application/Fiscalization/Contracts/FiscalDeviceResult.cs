namespace ZohoAmarco.Application.Fiscalization.Contracts;

public enum FiscalDeviceOutcome
{
    Succeeded =0,
    Failed = 1,
    OutcomeUnknown = 2
}

/// <summary>
/// Result returned by the fiscal device adapter.
/// </summary>
public sealed record FiscalDeviceResult(
    FiscalDeviceOutcome Outcome,
    string? FiscalReceiptNumber = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);