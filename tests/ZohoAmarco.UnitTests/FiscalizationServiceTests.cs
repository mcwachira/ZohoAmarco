
using ZohoAmarco.Application.Fiscalization;
using ZohoAmarco.Application.Fiscalization.Abstractions;
using ZohoAmarco.Application.Fiscalization.Contracts;
using ZohoAmarco.Domain.Fiscalization;

namespace ZohoAmarco.UnitTests.Fiscalization;

public sealed class FiscalizationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_ShouldSaveReceipt_WhenDeviceSucceeds()
    {
        var device = new FakeFiscalDevice
        {
            Result = new FiscalDeviceResult(
                FiscalDeviceOutcome.Succeeded,
                FiscalReceiptNumber: "RECEIPT-001")
        };

        var repository = new InMemoryFiscalizationRequestRepository();
        var service = CreateService(device, repository);

        var result = await service.ExecuteAsync(
            "zoho-invoice-123",
            "idempotency-123",
            CreateDocument());

        Assert.Equal(FiscalizationStatus.Succeeded, result.Status);
        Assert.Equal("RECEIPT-001", result.FiscalReceiptNumber);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Equal(1, device.CallCount);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordFailure_WhenDeviceFails()
    {
        var device = new FakeFiscalDevice
        {
            Result = new FiscalDeviceResult(
                FiscalDeviceOutcome.Failed,
                ErrorCode: "DEVICE_REJECTED",
                ErrorMessage: "The device rejected the invoice.")
        };

        var repository = new InMemoryFiscalizationRequestRepository();
        var service = CreateService(device, repository);

        var result = await service.ExecuteAsync(
            "zoho-invoice-123",
            "idempotency-123",
            CreateDocument());

        Assert.Equal(FiscalizationStatus.Failed, result.Status);
        Assert.Equal("DEVICE_REJECTED", result.FailureCode);
        Assert.Equal(
            "The device rejected the invoice.",
            result.FailureReason);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Equal(1, device.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordUnknownOutcome_WhenDeviceIsUncertain()
    {
        var device = new FakeFiscalDevice
        {
            Result = new FiscalDeviceResult(
                FiscalDeviceOutcome.OutcomeUnknown,
                ErrorCode: "DEVICE_TIMEOUT",
                ErrorMessage: "The device did not respond.")
        };

        var repository = new InMemoryFiscalizationRequestRepository();
        var service = CreateService(device, repository);

        var result = await service.ExecuteAsync(
            "zoho-invoice-123",
            "idempotency-123",
            CreateDocument());

        Assert.Equal(FiscalizationStatus.OutcomeUnknown, result.Status);
        Assert.Equal("DEVICE_TIMEOUT", result.FailureCode);
        Assert.Null(result.CompletedAtUtc);
        Assert.Equal(1, device.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordUnknownOutcome_WhenDeviceThrows()
    {
        var device = new FakeFiscalDevice
        {
            ExceptionToThrow = new TimeoutException(
                "The device response timed out.")
        };

        var repository = new InMemoryFiscalizationRequestRepository();
        var service = CreateService(device, repository);

        var result = await service.ExecuteAsync(
            "zoho-invoice-123",
            "idempotency-123",
            CreateDocument());

        Assert.Equal(FiscalizationStatus.OutcomeUnknown, result.Status);
        Assert.Equal("DEVICE_COMMUNICATION_ERROR", result.FailureCode);
        Assert.Null(result.CompletedAtUtc);
        Assert.Equal(1, device.CallCount);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCallDeviceAgain_WhenKeyIsReused()
    {
        var device = new FakeFiscalDevice
        {
            Result = new FiscalDeviceResult(
                FiscalDeviceOutcome.Succeeded,
                FiscalReceiptNumber: "RECEIPT-002")
        };

        var repository = new InMemoryFiscalizationRequestRepository();
        var service = CreateService(device, repository);

        var first = await service.ExecuteAsync(
            "zoho-invoice-123",
            "idempotency-123",
            CreateDocument());

        var second = await service.ExecuteAsync(
            "zoho-invoice-123",
            "idempotency-123",
            CreateDocument());

        Assert.Equal(FiscalizationStatus.Succeeded, first.Status);
        Assert.Equal(FiscalizationStatus.Succeeded, second.Status);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, device.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCallDevice_WhenAnotherWorkerClaimsRequest()
    {
        var device = new FakeFiscalDevice
        {
            Result = new FiscalDeviceResult(
                FiscalDeviceOutcome.Succeeded,
                FiscalReceiptNumber: "SHOULD-NOT-BE-ISSUED")
        };

        var repository = new InMemoryFiscalizationRequestRepository
        {
            SimulateClaimByAnotherWorker = true
        };

        var service = CreateService(device, repository);

        var result = await service.ExecuteAsync(
            "zoho-invoice-123",
            "idempotency-123",
            CreateDocument());

        Assert.Equal(FiscalizationStatus.InProgress, result.Status);
        Assert.Equal(0, device.CallCount);
    }
    
    
[Fact]
public async Task GetStaleInProgressAsync_ShouldReturnOldInProgressRequests()
{
    var repository = new InMemoryFiscalizationRequestRepository();
    var cutoff = Now.AddMinutes(-10);

    var staleRequest = new FiscalizationRequest(
        Guid.NewGuid(),
        "invoice-stale",
        "key-stale",
        Now.AddMinutes(-30));

    staleRequest.StartProcessing(Now.AddMinutes(-20));

    await repository.GetOrCreateAsync(staleRequest);

    var results = await repository.GetStaleInProgressAsync(cutoff);

    var result = Assert.Single(results);
    Assert.Equal(staleRequest.Id, result.Id);
}

[Fact]
public async Task GetStaleInProgressAsync_ShouldExcludeRecentInProgressRequests()
{
    var repository = new InMemoryFiscalizationRequestRepository();
    var cutoff = Now.AddMinutes(-10);

    var recentRequest = new FiscalizationRequest(
        Guid.NewGuid(),
        "invoice-recent",
        "key-recent",
        Now.AddMinutes(-5));

    recentRequest.StartProcessing(Now.AddMinutes(-5));

    await repository.GetOrCreateAsync(recentRequest);

    var results = await repository.GetStaleInProgressAsync(cutoff);

    Assert.Empty(results);
}

[Fact]
public async Task GetStaleInProgressAsync_ShouldExcludeRequestsWithoutProcessingTimestamp()
{
    var repository = new InMemoryFiscalizationRequestRepository();

    var pendingRequest = new FiscalizationRequest(
        Guid.NewGuid(),
        "invoice-pending",
        "key-pending",
        Now.AddHours(-1));

    await repository.GetOrCreateAsync(pendingRequest);

    var results = await repository.GetStaleInProgressAsync(Now);

    Assert.Empty(results);
}

[Fact]
public async Task GetStaleInProgressAsync_ShouldExcludeCompletedRequests()
{
    var repository = new InMemoryFiscalizationRequestRepository();

    var succeededRequest = new FiscalizationRequest(
        Guid.NewGuid(),
        "invoice-completed",
        "key-completed",
        Now.AddHours(-1));

    succeededRequest.StartProcessing(Now.AddMinutes(-30));
    succeededRequest.MarkSucceeded(Now.AddMinutes(-29), "RECEIPT-TEST");

    await repository.GetOrCreateAsync(succeededRequest);

    var results = await repository.GetStaleInProgressAsync(Now);

    Assert.Empty(results);
}

[Fact]
public async Task GetStaleInProgressAsync_ShouldReturnOldestRequestFirst()
{
    var repository = new InMemoryFiscalizationRequestRepository();
    var cutoff = Now.AddMinutes(-10);

    var newerRequest = new FiscalizationRequest(
        Guid.NewGuid(),
        "invoice-newer",
        "key-newer",
        Now.AddMinutes(-40));

    newerRequest.StartProcessing(Now.AddMinutes(-20));

    var olderRequest = new FiscalizationRequest(
        Guid.NewGuid(),
        "invoice-older",
        "key-older",
        Now.AddMinutes(-50));

    olderRequest.StartProcessing(Now.AddMinutes(-30));

    await repository.GetOrCreateAsync(newerRequest);
    await repository.GetOrCreateAsync(olderRequest);

    var results = await repository.GetStaleInProgressAsync(cutoff);

    Assert.Equal(2, results.Count);
    Assert.Equal(olderRequest.Id, results[0].Id);
    Assert.Equal(newerRequest.Id, results[1].Id);
}


    private static FiscalizationService CreateService(
        IFiscalDevice device,
        IFiscalizationRequestRepository repository)
    {
        return new FiscalizationService(
            device,
            repository,
            new FixedTimeProvider(Now));
    }

    private static FiscalizationDocument CreateDocument()
    {
        return new FiscalizationDocument(
            ZohoInvoiceId: "zoho-invoice-123",
            InvoiceNumber: "INV-001",
            IssueDate: new DateOnly(2026, 10, 21),
            CurrencyCode: "KES",
            CustomerName: "Test Customer",
            Lines:
            [
                new FiscalizationLine(
                    Description: "Test item",
                    Quantity: 1,
                    UnitPrice: 100,
                    TaxRate: 16,
                    TaxAmount: 16)
            ]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeFiscalDevice : IFiscalDevice
    {
        public FiscalDeviceResult Result { get; set; } =
            new(FiscalDeviceOutcome.Succeeded, "RECEIPT-DEFAULT");

        public Exception? ExceptionToThrow { get; set; }

        public int CallCount { get; private set; }

        public Task<FiscalDeviceResult> FiscalizeAsync(
            FiscalizationDocument document,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class InMemoryFiscalizationRequestRepository
        : IFiscalizationRequestRepository
    {
        private readonly Dictionary<string, FiscalizationRequest> _requests =
            new(StringComparer.Ordinal);

        public int SaveCount { get; private set; }

        public bool SimulateClaimByAnotherWorker { get; set; }

        public Task<FiscalizationRequest> GetOrCreateAsync(
            FiscalizationRequest candidate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_requests.TryGetValue(candidate.IdempotencyKey, out var existing))
            {
                return Task.FromResult(existing);
            }

            _requests.Add(candidate.IdempotencyKey, candidate);

            return Task.FromResult(candidate);
        }

        public Task<FiscalizationRequest?> TryStartProcessingAsync(
            Guid requestId,
            DateTimeOffset atUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = _requests.Values.SingleOrDefault(
                item => item.Id == requestId);

            if (request is null ||
                request.Status != FiscalizationStatus.Pending)
            {
                return Task.FromResult<FiscalizationRequest?>(null);
            }

            if (SimulateClaimByAnotherWorker)
            {
                request.StartProcessing(atUtc);
                return Task.FromResult<FiscalizationRequest?>(null);
            }

            request.StartProcessing(atUtc);

            return Task.FromResult<FiscalizationRequest?>(request);
        }

        public Task<FiscalizationRequest?> GetByIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = _requests.Values.SingleOrDefault(
                item => item.Id == requestId);

            return Task.FromResult(request);
        }

        public Task SaveAsync(
            FiscalizationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_requests.ContainsKey(request.IdempotencyKey))
            {
                throw new InvalidOperationException(
                    "Cannot save a request that has not been registered.");
            }

            SaveCount++;

            return Task.CompletedTask;
        }
        
        
        public Task<IReadOnlyList<FiscalizationRequest>> GetStaleInProgressAsync(
            DateTimeOffset startedBeforeUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<FiscalizationRequest> staleRequests = _requests.Values
                .Where(request =>
                    request.Status == FiscalizationStatus.InProgress &&
                    request.ProcessingStartedAtUtc.HasValue &&
                    request.ProcessingStartedAtUtc.Value <= startedBeforeUtc)
                .OrderBy(request => request.ProcessingStartedAtUtc)
                .ToList();

            return Task.FromResult(staleRequests);
        }

    }
}
