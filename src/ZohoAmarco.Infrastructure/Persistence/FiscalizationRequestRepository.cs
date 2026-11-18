using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ZohoAmarco.Application.Fiscalization.Abstractions;
using ZohoAmarco.Domain.Fiscalization;

namespace ZohoAmarco.Infrastructure.Persistence;

public sealed class FiscalizationRequestRepository(
    FiscalizationDbContext dbContext)
    : IFiscalizationRequestRepository
{
    public async Task<FiscalizationRequest> GetOrCreateAsync(
        FiscalizationRequest candidate,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.FiscalizationRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.IdempotencyKey == candidate.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        dbContext.FiscalizationRequests.Add(candidate);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            // Return the persisted entity without leaving it tracked.
            // Later repository calls may load another instance of this row.
            dbContext.Entry(candidate).State = EntityState.Detached;

            return candidate;
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            // Another request may have inserted the same key concurrently.
            // Clear the failed tracked insert before querying the winner.
            dbContext.Entry(candidate).State = EntityState.Detached;

            var winner = await dbContext.FiscalizationRequests
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.IdempotencyKey == candidate.IdempotencyKey,
                    cancellationToken);

            if (winner is not null)
            {
                return winner;
            }

            throw;
        }
    }


    public async Task<FiscalizationRequest?> TryStartProcessingAsync(
        Guid requestId,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken = default)
    {
        // Atomically claim the request only if it is still Pending.
        var affectedRows = await dbContext.FiscalizationRequests
            .Where(x =>
                x.Id == requestId &&
                x.Status == FiscalizationStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.Status,
                        FiscalizationStatus.InProgress)
                    .SetProperty(
                        x => x.UpdatedAtUtc,
                        atUtc)
                    .SetProperty(
                        x => x.ProcessingStartedAtUtc,
                        (DateTimeOffset?)atUtc),
                cancellationToken);

        // Zero affected rows means the request was missing or already claimed.
        if (affectedRows == 0)
        {
            return null;
        }

        // Reload the persisted state without tracking it.
        return await dbContext.FiscalizationRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == requestId,
                cancellationToken);
    }


    public Task<FiscalizationRequest?> GetByIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FiscalizationRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == requestId,
                cancellationToken);
    }

    
    public async Task<IReadOnlyList<FiscalizationRequest>> GetStaleInProgressAsync(
        DateTimeOffset startedBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.FiscalizationRequests
            .AsNoTracking()
            .Where(x =>
                x.Status == FiscalizationStatus.InProgress &&
                x.ProcessingStartedAtUtc != null &&
                x.ProcessingStartedAtUtc <= startedBeforeUtc)
            .OrderBy(x => x.ProcessingStartedAtUtc)
            .ToListAsync(cancellationToken);
    }

  
    public async Task SaveAsync(
        FiscalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = dbContext.Entry(request);

        if (entry.State == EntityState.Detached)
        {
            dbContext.FiscalizationRequests.Attach(request);
            entry = dbContext.Entry(request);
        }

        if (entry.State == EntityState.Added)
        {
            throw new InvalidOperationException(
                "Cannot update a fiscalization request that has not been persisted.");
        }

        // Only mark fields that a fiscalization state transition can change.
        // Immutable identity and creation fields remain untouched.
        entry.Property(x => x.Status).IsModified = true;
        entry.Property(x => x.UpdatedAtUtc).IsModified = true;
        entry.Property(x => x.CompletedAtUtc).IsModified = true;
        entry.Property(x => x.FiscalReceiptNumber).IsModified = true;
        entry.Property(x => x.FailureCode).IsModified = true;
        entry.Property(x => x.FailureReason).IsModified = true;

        // RowVersion is a database-generated concurrency token.
        // EF Core uses its original value in the UPDATE predicate.
        // SQL Server generates the new value after a successful update.
        await dbContext.SaveChangesAsync(cancellationToken);
    }


    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Number is 2601 or 2627;
    }
}
