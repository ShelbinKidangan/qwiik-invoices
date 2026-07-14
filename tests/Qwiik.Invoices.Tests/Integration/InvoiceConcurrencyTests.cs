using System.Net;
using System.Net.Http.Json;
using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// Optimistic concurrency rides the real SQL Server <c>rowversion</c>: a successful status
/// change bumps the token, so replaying the original (now stale) token is rejected as a
/// 409 ProblemDetails. This proves the concurrency token actually changes on the database.
/// </summary>
public sealed class InvoiceConcurrencyTests : IntegrationTestBase
{
    public InvoiceConcurrencyTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ChangeStatus_WithStaleRowVersion_Returns409()
    {
        var tenantId = Guid.NewGuid();
        var created = await CreateInvoiceAsync(tenantId); // starts Draft
        created.Status.Should().Be(nameof(InvoiceStatus.Draft));

        using var client = Factory.ClientForTenant(tenantId);

        // First move (Draft -> Sent) with the create's token succeeds and returns a fresh
        // token — SQL Server has bumped the real rowversion on the persisted row.
        var firstResponse = await client.PatchAsJsonAsync(
            $"/api/v1/invoices/{created.Id}/status",
            new UpdateStatusRequest(InvoiceStatus.Sent, created.RowVersion));

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await firstResponse.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;
        updated.Status.Should().Be(nameof(InvoiceStatus.Sent));
        updated.RowVersion.Should().NotBe(created.RowVersion,
            "a successful write bumps the SQL Server rowversion token");

        // Replaying the ORIGINAL token (Sent -> Paid is a legal move) must lose to the
        // bumped rowversion: the write is a concurrency conflict, not a transition error.
        var staleResponse = await client.PatchAsJsonAsync(
            $"/api/v1/invoices/{created.Id}/status",
            new UpdateStatusRequest(InvoiceStatus.Paid, created.RowVersion));

        staleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await AssertProblemDetailsAsync(staleResponse, (int)HttpStatusCode.Conflict);
    }
}
