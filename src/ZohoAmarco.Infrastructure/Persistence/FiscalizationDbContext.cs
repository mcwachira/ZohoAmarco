using Microsoft.EntityFrameworkCore;
using ZohoAmarco.Domain.Fiscalization;

namespace ZohoAmarco.Infrastructure.Persistence;

public sealed class FiscalizationDbContext(
    DbContextOptions<FiscalizationDbContext> options)
    : DbContext(options)
{
    public DbSet<FiscalizationRequest> FiscalizationRequests =>
        Set<FiscalizationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var request = modelBuilder.Entity<FiscalizationRequest>();

        request.ToTable("fiscalization_requests");
        request.HasKey(x => x.Id);

        request.Property(x => x.Id)
            .ValueGeneratedNever();

        request.Property(x => x.ZohoInvoiceId)
            .HasMaxLength(100)
            .IsRequired();

        request.Property(x => x.IdempotencyKey)
            .HasMaxLength(200)
            .IsRequired();

        request.HasIndex(x => x.IdempotencyKey)
            .IsUnique();

        request.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        request.Property(x => x.CreatedAtUtc).IsRequired();
        request.Property(x => x.UpdatedAtUtc).IsRequired();
        request.Property(x => x.ProcessingStartedAtUtc);

        request.Property(x => x.RowVersion)
            .IsRowVersion();

        request.Property(x => x.FiscalReceiptNumber)
            .HasMaxLength(100);

        request.Property(x => x.FailureCode)
            .HasMaxLength(100);

        request.Property(x => x.FailureReason)
            .HasMaxLength(2000);
    }
}