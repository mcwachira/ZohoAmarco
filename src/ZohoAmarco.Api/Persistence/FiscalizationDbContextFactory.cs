using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ZohoAmarco.Infrastructure.Persistence;

namespace ZohoAmarco.Api.Persistence;

/// <summary>
/// Creates FiscalizationDbContext for EF Core design-time operations,
/// such as generating and applying migrations.
/// This does not register or invoke the physical fiscal device.
/// </summary>

public sealed class FiscalizationDbContextFactory
    : IDesignTimeDbContextFactory<FiscalizationDbContext>
{
    public FiscalizationDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString =
            builder.Configuration.GetConnectionString("FiscalizationDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'FiscalizationDatabase' is missing.");

        var options = new DbContextOptionsBuilder<FiscalizationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new FiscalizationDbContext(options);
    }
}