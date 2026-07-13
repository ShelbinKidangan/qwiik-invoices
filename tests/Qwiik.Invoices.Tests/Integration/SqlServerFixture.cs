using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Infrastructure;
using Testcontainers.MsSql;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// Provisions a real SQL Server for the integration suite and applies the actual EF
/// migration once. If <c>QWIIK_TEST_SQL</c> is set (e.g. a LocalDB connection string) it
/// is used directly — the no-Docker fallback; otherwise a Testcontainers MsSql container
/// is started. Shared across the whole collection so the container spins up only once.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string ConnectionStringEnvVar = "QWIIK_TEST_SQL";

    private readonly MsSqlContainer? _container;

    public SqlServerFixture()
    {
        // No env override -> run against a throwaway container. Build it in the ctor so
        // ConnectionString is available, but don't start it until InitializeAsync.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvVar)))
            _container = new MsSqlBuilder().Build();
    }

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        else
        {
            ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar)!;
        }

        // Apply the real migration — the same schema production runs, not EnsureCreated.
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Truncates all invoice rows between tests. The FK from line items to invoices
    /// cascades, so deleting the parents clears the children too.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Invoices");
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    // A tenant-less context for schema/maintenance work. The global query filter reads
    // TenantContext.TenantId; for MigrateAsync and the DELETE it is never evaluated.
    private InvoiceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InvoiceDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new InvoiceDbContext(options, new TenantContext());
    }
}
