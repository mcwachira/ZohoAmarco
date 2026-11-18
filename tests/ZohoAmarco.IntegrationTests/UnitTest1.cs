
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZohoAmarco.Application.Fiscalization;
using ZohoAmarco.Api;
using ZohoAmarco.Application.Fiscalization.Abstractions;
using ZohoAmarco.Domain.Fiscalization;
using ZohoAmarco.Infrastructure.Persistence;

namespace ZohoAmarco.IntegrationTests;

public sealed class FiscalizationRequestRepositoryTests
{
    private const string TestDatabaseName =
        "ZohoAmarcoIntegrationTestsDb";

    private static string GetTestConnectionString()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ZOHOAMARCO_TEST_CONNECTION_STRING")
            ?? @"Server=.\SQLEXPRESS;Database=ZohoAmarcoIntegrationTestsDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";

        var builder = new SqlConnectionStringBuilder(connectionString);

        // Fail safely if someone accidentally configures the development
        // database or the customer's SYSPRO database for these tests.
        if (!string.Equals(
                builder.InitialCatalog,
                TestDatabaseName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Integration tests may only use '{TestDatabaseName}'. " +
                $"Configured database: '{builder.InitialCatalog}'.");
        }

        return builder.ConnectionString;
    }

    private static FiscalizationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FiscalizationDbContext>()
            .UseSqlServer(GetTestConnectionString())
            .Options;

        return new FiscalizationDbContext(options);
    }

    private static async Task InitializeDatabaseAsync()
    {
        await using var dbContext = CreateDbContext();

        // Applies pending EF Core migrations. It does not delete the database.
        await dbContext.Database.MigrateAsync();
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldPersistRequest_AndAllowItToBeRetrieved()
    {
        await InitializeDatabaseAsync();

        var now = DateTimeOffset.UtcNow;
        var candidate = new FiscalizationRequest(
            Guid.NewGuid(),
            $"zoho-invoice-{Guid.NewGuid():N}",
            $"idempotency-{Guid.NewGuid():N}",
            now);

        Guid savedId;

        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);

            var saved = await repository.GetOrCreateAsync(candidate);

            savedId = saved.Id;

            Assert.Equal(candidate.Id, saved.Id);
            Assert.Equal(candidate.ZohoInvoiceId, saved.ZohoInvoiceId);
            Assert.Equal(candidate.IdempotencyKey, saved.IdempotencyKey);
            Assert.Equal(FiscalizationStatus.Pending, saved.Status);
        }

        // A new DbContext verifies the data came from SQL Server,
        // rather than merely being tracked in the original context.
        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);

            var retrieved = await repository.GetByIdAsync(savedId);

            Assert.NotNull(retrieved);
            Assert.Equal(savedId, retrieved.Id);
            Assert.Equal(FiscalizationStatus.Pending, retrieved.Status);
        }
    }

    [Fact]
    public async Task GetOrCreateAsync_ShouldReturnExistingRequest_ForDuplicateIdempotencyKey()
    {
        await InitializeDatabaseAsync();

        var idempotencyKey = $"idempotency-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var original = new FiscalizationRequest(
            Guid.NewGuid(),
            $"original-invoice-{Guid.NewGuid():N}",
            idempotencyKey,
            now);

        var duplicate = new FiscalizationRequest(
            Guid.NewGuid(),
            $"different-invoice-{Guid.NewGuid():N}",
            idempotencyKey,
            now.AddMinutes(1));

        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);

            var created = await repository.GetOrCreateAsync(original);

            Assert.Equal(original.Id, created.Id);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);

            var returned = await repository.GetOrCreateAsync(duplicate);

            Assert.Equal(original.Id, returned.Id);
            Assert.Equal(original.ZohoInvoiceId, returned.ZohoInvoiceId);
            Assert.Equal(idempotencyKey, returned.IdempotencyKey);
        }

        await using (var dbContext = CreateDbContext())
        {
            var count = await dbContext.FiscalizationRequests
                .CountAsync(x => x.IdempotencyKey == idempotencyKey);

            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task TryStartProcessingAsync_ShouldAllowOnlyOneWorkerToClaimRequest()
    {
        await InitializeDatabaseAsync();

        var now = DateTimeOffset.UtcNow;
        var candidate = new FiscalizationRequest(
            Guid.NewGuid(),
            $"zoho-invoice-{Guid.NewGuid():N}",
            $"idempotency-{Guid.NewGuid():N}",
            now);

        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);

            await repository.GetOrCreateAsync(candidate);
        }

        // Each worker must have its own DbContext.
        // EF Core DbContext instances are not thread-safe.
        var workerTasks = Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var dbContext = CreateDbContext();
            var repository = new FiscalizationRequestRepository(dbContext);

            return await repository.TryStartProcessingAsync(
                candidate.Id,
                now.AddSeconds(1));
        });

        var results = await Task.WhenAll(workerTasks);

        // Exactly one worker should change Pending to InProgress.
       
        // Exactly one worker should successfully claim the request.
        Assert.Single(results, result => result is not null);

        // The other worker should fail to claim the already-claimed request.
        Assert.Single(results, result => result is null);


        await using (var dbContext = CreateDbContext())
        {
            var persisted = await dbContext.FiscalizationRequests
                .AsNoTracking()
                .SingleAsync(x => x.Id == candidate.Id);

            Assert.Equal(FiscalizationStatus.InProgress, persisted.Status);
        }
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistSuccessfulFiscalization()
    {
        await InitializeDatabaseAsync();

        var now = DateTimeOffset.UtcNow;
        var candidate = new FiscalizationRequest(
            Guid.NewGuid(),
            $"zoho-invoice-{Guid.NewGuid():N}",
            $"idempotency-{Guid.NewGuid():N}",
            now);

        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);

            await repository.GetOrCreateAsync(candidate);
        }

        FiscalizationRequest processingRequest;

        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);

            var claimed = await repository.TryStartProcessingAsync(
                candidate.Id,
                now.AddSeconds(1));

            Assert.NotNull(claimed);
            processingRequest = claimed;
        }

        var completionTime = now.AddSeconds(5);
        processingRequest.MarkSucceeded(
            completionTime,
            "TEST-RECEIPT-001");

        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);

            await repository.SaveAsync(processingRequest);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new FiscalizationRequestRepository(dbContext);
            var persisted = await repository.GetByIdAsync(candidate.Id);

            Assert.NotNull(persisted);
            Assert.Equal(FiscalizationStatus.Succeeded, persisted.Status);
            Assert.Equal("TEST-RECEIPT-001", persisted.FiscalReceiptNumber);
            Assert.Equal(completionTime, persisted.CompletedAtUtc);
            Assert.Null(persisted.FailureCode);
            Assert.Null(persisted.FailureReason);
        }
    }
    
    
[Fact]
public async Task SaveAsync_ShouldRejectStaleUpdate_WhenRowVersionHasChanged()
{
    await InitializeDatabaseAsync();

    var now = DateTimeOffset.UtcNow;

    var candidate = new FiscalizationRequest(
        Guid.NewGuid(),
        $"zoho-invoice-{Guid.NewGuid():N}",
        $"idempotency-{Guid.NewGuid():N}",
        now);

    // Arrange: persist a new request.
    await using (var dbContext = CreateDbContext())
    {
        var repository = new FiscalizationRequestRepository(dbContext);
        await repository.GetOrCreateAsync(candidate);
    }

    // Move the request to InProgress.
    await using (var dbContext = CreateDbContext())
    {
        var repository = new FiscalizationRequestRepository(dbContext);

        var claimed = await repository.TryStartProcessingAsync(
            candidate.Id,
            now.AddSeconds(1));

        Assert.NotNull(claimed);
    }

    // Simulate two workers loading the same database version.
    FiscalizationRequest workerARequest;
    FiscalizationRequest workerBRequest;

    await using (var dbContext = CreateDbContext())
    {
        var repository = new FiscalizationRequestRepository(dbContext);

        workerARequest = (await repository.GetByIdAsync(candidate.Id))!;
    }

    await using (var dbContext = CreateDbContext())
    {
        var repository = new FiscalizationRequestRepository(dbContext);

        workerBRequest = (await repository.GetByIdAsync(candidate.Id))!;
    }

    // Both workers should have loaded the same row version.
    Assert.Equal(workerARequest.RowVersion, workerBRequest.RowVersion);
    Assert.Equal(FiscalizationStatus.InProgress, workerARequest.Status);
    Assert.Equal(FiscalizationStatus.InProgress, workerBRequest.Status);

    // Worker A saves success first.
    workerARequest.MarkSucceeded(
        now.AddSeconds(5),
        "TEST-RECEIPT-002");

    await using (var dbContext = CreateDbContext())
    {
        var repository = new FiscalizationRequestRepository(dbContext);
        await repository.SaveAsync(workerARequest);
    }

    // Worker B still has the old RowVersion and attempts a conflicting update.
    workerBRequest.MarkFailed(
        now.AddSeconds(6),
        "DEVICE_ERROR",
        "Simulated device failure.");

    await using (var dbContext = CreateDbContext())
    {
        var repository = new FiscalizationRequestRepository(dbContext);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => repository.SaveAsync(workerBRequest));
    }

    // Verify that worker B did not overwrite worker A's successful result.
    await using (var dbContext = CreateDbContext())
    {
        var repository = new FiscalizationRequestRepository(dbContext);
        var persisted = await repository.GetByIdAsync(candidate.Id);

        Assert.NotNull(persisted);
        Assert.Equal(FiscalizationStatus.Succeeded, persisted.Status);
        Assert.Equal("TEST-RECEIPT-002", persisted.FiscalReceiptNumber);
        Assert.Null(persisted.FailureCode);
    }
}


[Fact]
public async Task GetOrCreateAndTryStartProcessing_ShouldWorkWithSameDbContext()
{
    await InitializeDatabaseAsync();

    var now = DateTimeOffset.UtcNow;

    var candidate = new FiscalizationRequest(
        Guid.NewGuid(),
        $"zoho-invoice-{Guid.NewGuid():N}",
        $"idempotency-{Guid.NewGuid():N}",
        now);

    await using var dbContext = CreateDbContext();

    var repository = new FiscalizationRequestRepository(dbContext);

    // Insert and retrieve the persisted request through this context.
    var created = await repository.GetOrCreateAsync(candidate);

    Assert.Equal(candidate.Id, created.Id);
    Assert.Equal(FiscalizationStatus.Pending, created.Status);

    // Claim the request using the same repository and DbContext.
    var claimed = await repository.TryStartProcessingAsync(
        candidate.Id,
        now.AddSeconds(1));

    Assert.NotNull(claimed);
    Assert.Equal(candidate.Id, claimed.Id);
    Assert.Equal(FiscalizationStatus.InProgress, claimed.Status);
}
 



}
