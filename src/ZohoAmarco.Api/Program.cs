using Microsoft.EntityFrameworkCore;
using ZohoAmarco.Application.Fiscalization;
using ZohoAmarco.Application.Fiscalization.Abstractions;
using ZohoAmarco.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Register OpenAPI support.
builder.Services.AddOpenApi();

// Register an injectable UTC time provider.
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// Configure SQL Server persistence.
var connectionString =
    builder.Configuration.GetConnectionString("FiscalizationDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'FiscalizationDatabase' is missing.");

builder.Services.AddDbContext<FiscalizationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register the persistence repository.
builder.Services.AddScoped<
    IFiscalizationRequestRepository,
    FiscalizationRequestRepository>();

// Register the application service.
// The physical fiscal device is intentionally not registered yet.
builder.Services.AddScoped<FiscalizationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// This basic health endpoint does not check database or device connectivity.
app.MapGet("/health", () => Results.Ok(new
    {
        Status = "Healthy",
        Application = "ZohoAmarco.Api"
    }))
    .WithName("HealthCheck");

app.Run();