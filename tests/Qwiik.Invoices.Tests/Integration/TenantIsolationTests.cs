using System.Net;
using System.Net.Http.Json;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// Proves the EF global query filter isolates tenants on real SQL semantics: one tenant
/// can see and act on its own invoice while another cannot, though both share the DB.
/// </summary>
public sealed class TenantIsolationTests : IntegrationTestBase
{
    public TenantIsolationTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_InvoiceOfAnotherTenant_ReturnsNotFound()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var created = await CreateInvoiceAsync(tenantA);

        // Owner sees it...
        using var clientA = Factory.ClientForTenant(tenantA);
        var ownerGet = await clientA.GetAsync($"/api/v1/invoices/{created.Id}");
        ownerGet.StatusCode.Should().Be(HttpStatusCode.OK);

        // ...the other tenant does not.
        using var clientB = Factory.ClientForTenant(tenantB);
        var otherGet = await clientB.GetAsync($"/api/v1/invoices/{created.Id}");
        otherGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_DoesNotIncludeOtherTenantsInvoices()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var created = await CreateInvoiceAsync(tenantA);

        using var clientB = Factory.ClientForTenant(tenantB);
        var page = await clientB.GetFromJsonAsync<PagedResult<InvoiceListItemResponse>>(
            "/api/v1/invoices", Json);

        page.Should().NotBeNull();
        page!.Items.Should().NotContain(i => i.Id == created.Id);
    }

    [Fact]
    public async Task ChangeStatus_OnAnotherTenantsInvoice_ReturnsNotFound()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var created = await CreateInvoiceAsync(tenantA);

        // Write-side isolation: tenant B cannot transition tenant A's invoice.
        using var clientB = Factory.ClientForTenant(tenantB);
        var request = new UpdateStatusRequest(InvoiceStatus.Sent, created.RowVersion);
        var response = await clientB.PatchAsJsonAsync(
            $"/api/v1/invoices/{created.Id}/status", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // And the invoice is untouched for its real owner.
        using var clientA = Factory.ClientForTenant(tenantA);
        var owned = await clientA.GetFromJsonAsync<InvoiceResponse>(
            $"/api/v1/invoices/{created.Id}", Json);
        owned!.Status.Should().Be(nameof(InvoiceStatus.Draft));
    }
}
