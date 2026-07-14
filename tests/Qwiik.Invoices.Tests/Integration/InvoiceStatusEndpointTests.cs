using System.Net;
using System.Net.Http.Json;
using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// The status endpoint enforces the domain transition rules: a legal move succeeds and
/// returns the new status, while an illegal one is rejected as a 400 ProblemDetails.
/// </summary>
public sealed class InvoiceStatusEndpointTests : IntegrationTestBase
{
    public InvoiceStatusEndpointTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ChangeStatus_LegalTransition_Returns200AndNewStatus()
    {
        var tenantId = Guid.NewGuid();
        var created = await CreateInvoiceAsync(tenantId); // starts Draft
        created.Status.Should().Be(nameof(InvoiceStatus.Draft));

        using var client = Factory.ClientForTenant(tenantId);
        var request = new UpdateStatusRequest(InvoiceStatus.Sent, created.RowVersion);
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/invoices/{created.Id}/status", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;
        updated.Status.Should().Be(nameof(InvoiceStatus.Sent));
    }

    [Fact]
    public async Task ChangeStatus_IllegalTransition_Returns400()
    {
        var tenantId = Guid.NewGuid();
        var created = await CreateInvoiceAsync(tenantId); // Draft cannot jump straight to Paid

        using var client = Factory.ClientForTenant(tenantId);
        var request = new UpdateStatusRequest(InvoiceStatus.Paid, created.RowVersion);
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/invoices/{created.Id}/status", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
