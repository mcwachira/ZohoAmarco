namespace ZohoAmarco.Domain.Fiscalization;

/// <summary>
/// Tracks one attempt to fiscalize a Zoho Books invoice.
///
/// Persistence, HTTP requests, Zoho API calls and Tremol SDK calls
/// do not belong in this domain entity.
/// </summary>
///

public sealed class FiscalizationRequest
{
    public Guid Id { get; private set; }
    public string ZohoInvoiceId { get; private set; }
    
    public string IdempotencyKey { get; private set; }
    
    public FiscalizationStatus Status { get; private set; }
    
    public byte[] RowVersion { get; private set; } = [];
    
    public  DateTimeOffset CreatedAtUtc { get; private set; }
    
    public DateTimeOffset? ProcessingStartedAtUtc { get; private set; }
    
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    
   
    
    
    ///<summary>
    /// Reference returned by the fiscal device, when available
    /// This is not ASSUMED TO BE THE kra/eTIMS Invoice Number
    /// </summary>
    
    public string? FiscalReceiptNumber { get; private set; }
    public string ? FailureCode { get; private set; }
    
    public string? FailureReason { get; private set; }


    private FiscalizationRequest()
    {
        ZohoInvoiceId = string.Empty;
        IdempotencyKey = string.Empty;
    }

    public FiscalizationRequest(
        Guid id, 
        string zohoInvoiceId, 
        string idempotencyKey,
        DateTimeOffset createdAtUtc
        )
    {

        if (id == Guid.Empty)
        {
            throw new ArgumentException(" Request id cannot be empty", nameof(id));
        }
        
        if(string.IsNullOrWhiteSpace(zohoInvoiceId))
        {
            throw new ArgumentException(" Zoho invoice id cannot be empty", nameof(zohoInvoiceId));
        }

        if(string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException(" Idempotency key cannot be empty", nameof(idempotencyKey));
        }

        Id = id;
        ZohoInvoiceId = zohoInvoiceId.Trim();
        IdempotencyKey = idempotencyKey.Trim();
        Status = FiscalizationStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Marks the request as being processed by the device workflow.
    /// Only a new Pending request may enter this state.
    /// </summary>
    ///



    public void StartProcessing(DateTimeOffset atUtc)
    {
        EnsureStatus(FiscalizationStatus.Pending);

        Status = FiscalizationStatus.InProgress;
        ProcessingStartedAtUtc = atUtc;
        UpdatedAtUtc = atUtc;
    }
    
    
    /// <summary>
    /// Records a confirmed successful fiscalization.
    /// </summary>
    
    
    public void MarkSucceeded(DateTimeOffset atUtc, string? fiscalReceiptNumber)
    {
        EnsureStatus(FiscalizationStatus.InProgress);
        if(string.IsNullOrWhiteSpace(fiscalReceiptNumber))
        {
            throw new ArgumentException(" Fiscal receipt number cannot be empty", nameof(fiscalReceiptNumber));
        }
        
        Status = FiscalizationStatus.Succeeded;
        FiscalReceiptNumber = fiscalReceiptNumber.Trim();
        FailureCode = null;
        FailureReason = null;
        CompletedAtUtc = atUtc;
        UpdatedAtUtc = atUtc;
    }

    /// <summary>
    /// Records a definitive failure, not a timeout with an unknown outcome.
    /// </summary>

    public void MarkFailed(DateTimeOffset atUtc, string failureCode, string failureReason)
    {
        EnsureStatus(FiscalizationStatus.InProgress);
        if (string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException(
                "A failure code is required.",
                nameof(failureCode));
        }

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException(
                "A failure reason is required.",
                nameof(failureReason));
        }

        Status = FiscalizationStatus.Failed;
        FailureCode = failureCode.Trim();
        FailureReason = failureReason.Trim();
        CompletedAtUtc = atUtc;
        UpdatedAtUtc = atUtc;
    }
    
    
    /// <summary>
    /// Records uncertainty when the device may have completed the command.
    /// This state must be reconciled before any further fiscal command.
    /// </summary>
    
    public void MarkOutcomeUnknown(
        DateTimeOffset atUtc,
        string failureCode,
        string failureReason)
    {
        EnsureStatus(FiscalizationStatus.InProgress);

        if (string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException(
                "A failure code is required.",
                nameof(failureCode));
        }

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException(
                "A reason for the unknown outcome is required.",
                nameof(failureReason));
        }

        Status = FiscalizationStatus.OutcomeUnknown;
        FailureCode = failureCode.Trim();
        FailureReason = failureReason.Trim();
        UpdatedAtUtc = atUtc;

        // Do not set CompletedAtUtc: the final outcome is not known.
    }
        
        private void EnsureStatus(FiscalizationStatus expectedStatus)
        {
            if (Status != expectedStatus)
            {
                throw new InvalidOperationException(
                    $"Cannot perform this operation when status is '{Status}'. " +
                    $"Expected status '{expectedStatus}'.");
            }
        }
    
    
}