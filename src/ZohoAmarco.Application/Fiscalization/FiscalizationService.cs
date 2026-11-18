    using ZohoAmarco.Application.Fiscalization.Abstractions;
    using ZohoAmarco.Application.Fiscalization.Contracts;
    using ZohoAmarco.Domain.Fiscalization;

    namespace ZohoAmarco.Application.Fiscalization;

    public sealed class FiscalizationService
    {
        private readonly IFiscalDevice _device;
        private readonly IFiscalizationRequestRepository _repository;
        private readonly TimeProvider _timeProvider;

        public FiscalizationService(

            IFiscalDevice device,
            IFiscalizationRequestRepository repository,
            TimeProvider timeProvider
        )
        {
            _device = device;
            _repository = repository;
            _timeProvider = timeProvider;
        }


        public async Task<FiscalizationRequest> ExecuteAsync(
            string zohoInvoiceId,
            string idempotenceKey,
            FiscalizationDocument document,
            CancellationToken cancellationToken = default
        )
        {

            ArgumentNullException.ThrowIfNull(document);

            ValidateDocument(document, zohoInvoiceId);

            var now = _timeProvider.GetUtcNow();

            var candidate = new FiscalizationRequest(
                Guid.NewGuid(),
                zohoInvoiceId,
                idempotenceKey,
                now
            );

            // The repository must atomically reuse an existing request
            // when the same idempotency key has already been registered.
            var existing = await _repository.GetOrCreateAsync(
                candidate,
                cancellationToken);



            // Never automatically repeat a request that has already
            // started or reached a final/uncertain state.
            if (existing.Status != FiscalizationStatus.Pending)
            {
                return existing;
            }

            
            // Only one worker may claim a Pending request.
            var request = await _repository.TryStartProcessingAsync(
                existing.Id,
                _timeProvider.GetUtcNow(),
                cancellationToken);
            //Only one worker may claim a Pending Request

            if (request is null)
            {
                // Another worker may have claimed it. Return the latest
                // state instead of issuing another device command.
                var latest = await _repository.GetByIdAsync(
                    existing.Id,
                    cancellationToken);

                return latest ?? throw new InvalidOperationException(
                    "The fiscalization request disappeared during processing.");
            }

            FiscalDeviceResult result;

            try
            {
                result = await _device.FiscalizeAsync(document, cancellationToken);
            }
            catch (Exception)
            {
                // Once a device operation has been attempted, an exception
                // does not prove that the device failed to issue a receipt.
                request.MarkOutcomeUnknown(
                    _timeProvider.GetUtcNow(),
                    "DEVICE_COMMUNICATION_ERROR",
                    "The device operation did not return a definitive result.");

                await _repository.SaveAsync(
                    request,
                    CancellationToken.None);

                return request;
            }


            var completedAt = _timeProvider.GetUtcNow();

            
            switch (result.Outcome)
            {
                case FiscalDeviceOutcome.Succeeded:
                    if (string.IsNullOrWhiteSpace(result.FiscalReceiptNumber))
                    {
                        request.MarkOutcomeUnknown(
                            completedAt,
                            "RECEIPT_REFERENCE_MISSING",
                            "The device reported success without a receipt reference.");
                    }
                    else
                    {
                        request.MarkSucceeded(
                            completedAt,
                            result.FiscalReceiptNumber);
                    }

                    break;

                case FiscalDeviceOutcome.Failed:
                    request.MarkFailed(
                        completedAt,
                        string.IsNullOrWhiteSpace(result.ErrorCode)
                            ? "DEVICE_REJECTED_REQUEST"
                            : result.ErrorCode,
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "The device reported a definitive fiscalization failure."
                            : result.ErrorMessage);

                    break;

                case FiscalDeviceOutcome.OutcomeUnknown:
                    request.MarkOutcomeUnknown(
                        completedAt,
                        string.IsNullOrWhiteSpace(result.ErrorCode)
                            ? "DEVICE_OUTCOME_UNKNOWN"
                            : result.ErrorCode,
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "The device outcome could not be confirmed."
                            : result.ErrorMessage);

                    break;

                default:
                    request.MarkOutcomeUnknown(
                        completedAt,
                        "INVALID_DEVICE_RESULT",
                        "The device adapter returned an unrecognized outcome.");

                    break;
            }


            // Persist the result even if the original HTTP request was
            // cancelled after the device operation began.
            await _repository.SaveAsync(
                request,
                CancellationToken.None);

            return request;
        }

        private static void ValidateDocument(
            FiscalizationDocument document,
            string zohoInvoiceId
        )
        {
            if (string.IsNullOrWhiteSpace(zohoInvoiceId))
            {
                throw new ArgumentException("The Zoho invoice ID is required.");
            }

            if (!string.Equals(
                    document.ZohoInvoiceId.Trim(),
                    zohoInvoiceId.Trim(),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The document invoice ID does not match the request invoice ID.",
                    nameof(document));
            }

            if (string.IsNullOrWhiteSpace(document.InvoiceNumber))
            {
                throw new ArgumentException(
                    "Invoice number is required.",
                    nameof(document));
            }

            if (document.CurrencyCode is not { Length: 3 } currency ||
                !currency.All(char.IsAsciiLetter))
            {
                throw new ArgumentException(
                    "Currency code must contain three ASCII letters.",
                    nameof(document));
            }

            if (document.Lines is null || document.Lines.Count == 0)
            {
                throw new ArgumentException(
                    "At least one invoice line is required.",
                    nameof(document));
            }


            foreach (var line in document.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.Description))
                {
                    throw new ArgumentException(
                        "Every invoice line requires a description.",
                        nameof(document));
                }

                if (line.Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Line quantity must be greater than zero.",
                        nameof(document));
                }

                if (line.UnitPrice < 0 ||
                    line.TaxRate < 0 ||
                    line.TaxAmount < 0)
                {
                    throw new ArgumentException(
                        "Price, tax rate, and tax amount cannot be negative.",
                        nameof(document));
                }
            }
        }
    }     