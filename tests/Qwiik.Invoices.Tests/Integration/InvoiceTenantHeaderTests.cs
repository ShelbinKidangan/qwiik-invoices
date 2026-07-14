using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// The tenant-resolution middleware rejects any request that cannot be tied to a valid
/// tenant before it reaches an endpoint, returning an RFC 7807 problem response.
/// </summary>
public sealed class InvoiceTenantHeaderTests : IntegrationTestBase
{
    public InvoiceTenantHeaderTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Create_WithMissingTenantHeader_Returns400()
    {
        // A raw client with no X-Tenant-Id header at all.
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/invoices", ValidCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Create_WithNonGuidTenantHeader_Returns400()
    {
        using var client = Factory.ClientWithRawTenantHeader("not-a-guid");

        var response = await client.PostAsJsonAsync("/api/v1/invoices", ValidCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Create_WithEmptyGuidTenantHeader_Returns400()
    {
        // Guid.Empty parses as a Guid but is rejected as a non-tenant.
        using var client = Factory.ClientWithRawTenantHeader(Guid.Empty.ToString());

        var response = await client.PostAsJsonAsync("/api/v1/invoices", ValidCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
    }
}
