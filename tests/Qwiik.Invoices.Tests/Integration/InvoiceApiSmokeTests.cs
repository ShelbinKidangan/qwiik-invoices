using System.Net;
using System.Net.Http.Json;
using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// End-to-end smoke test proving the harness works: a create round-trips through the real
/// pipeline to SQL Server, returns 201 with a Location header and server-computed totals,
/// and the row is then readable by id.
/// </summary>
public sealed class InvoiceApiSmokeTests : IntegrationTestBase
{
    public InvoiceApiSmokeTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Create_ValidInvoice_Returns201WithLocationAndPersists()
    {
        var tenantId = Guid.NewGuid();
        using var client = Factory.ClientForTenant(tenantId);

        var response = await client.PostAsJsonAsync("/api/v1/invoices", ValidCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = (await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;

        // Server computes the money: 2 x 100.00 @ 10% -> 200.00 + 20.00 = 220.00.
        created.Subtotal.Should().Be(200.00m);
        created.TaxTotal.Should().Be(20.00m);
        created.Total.Should().Be(220.00m);
        created.Status.Should().Be(nameof(InvoiceStatus.Draft));
        created.InvoiceNumber.Should().NotBeNullOrWhiteSpace();

        // The Location header points at the new resource, which is now retrievable.
        var fetched = await client.GetFromJsonAsync<InvoiceResponse>(
            $"/api/v1/invoices/{created.Id}", Json);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.Total.Should().Be(created.Total);
        fetched.CustomerName.Should().Be("Acme Corp");
        fetched.LineItems.Should().ContainSingle();
    }
}
