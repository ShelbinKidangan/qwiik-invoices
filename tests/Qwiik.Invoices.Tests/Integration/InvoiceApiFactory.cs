using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qwiik.Invoices.Api.Infrastructure;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// Boots the real application via <see cref="WebApplicationFactory{TEntryPoint}"/>,
/// swapping only the database to point at the test SQL Server. The full pipeline —
/// including the real tenant middleware and global query filter — stays intact, so the
/// tenant flows through exactly as it would in production.
/// </summary>
public sealed class InvoiceApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public InvoiceApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" is neither Development nor Production, so the Scalar/OpenApi mapping
        // guarded by IsDevelopment() is skipped and no doc pipeline is wired up.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Drop the app's DbContext registration and re-point it at the test database.
            // Note: the DbContext is NOT overridden — only its options — so the real
            // tenant context and query filter still apply.
            var descriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<InvoiceDbContext>));
            services.Remove(descriptor);

            services.AddDbContext<InvoiceDbContext>(o => o.UseSqlServer(_connectionString));
        });
    }

    /// <summary>
    /// An <see cref="HttpClient"/> whose requests carry <paramref name="tenantId"/> in the
    /// <c>X-Tenant-Id</c> header, so the tenant is resolved by the real middleware.
    /// </summary>
    public HttpClient ClientForTenant(Guid tenantId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }
}
