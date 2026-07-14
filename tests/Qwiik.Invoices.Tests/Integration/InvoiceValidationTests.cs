using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// The FluentValidation layer produces a field-level 400 <c>ProblemDetails</c> at the API
/// boundary before the aggregate is ever constructed, and an unknown id returns 404.
/// </summary>
public sealed class InvoiceValidationTests : IntegrationTestBase
{
    public InvoiceValidationTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Create_WithMalformedEmail_Returns400()
    {
        var tenantId = Guid.NewGuid();
        using var client = Factory.ClientForTenant(tenantId);

        // Email-format is enforced ONLY by the validator — the domain checks non-empty.
        var request = ValidCreateRequest() with { CustomerEmail = "not-an-email" };

        var response = await client.PostAsJsonAsync("/api/v1/invoices", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().Should().Contain("email");
    }

    [Fact]
    public async Task Create_WithNoLineItems_Returns400()
    {
        var tenantId = Guid.NewGuid();
        using var client = Factory.ClientForTenant(tenantId);

        var request = ValidCreateRequest() with { LineItems = Array.Empty<CreateLineItemRequest>() };

        var response = await client.PostAsJsonAsync("/api/v1/invoices", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Create_WithDueDateBeforeIssueDate_Returns400()
    {
        var tenantId = Guid.NewGuid();
        using var client = Factory.ClientForTenant(tenantId);

        var request = ValidCreateRequest() with
        {
            IssueDate = new DateOnly(2026, 1, 31),
            DueDate = new DateOnly(2026, 1, 1),
        };

        var response = await client.PostAsJsonAsync("/api/v1/invoices", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertProblemDetailsAsync(response, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        using var client = Factory.ClientForTenant(tenantId);

        var response = await client.GetAsync($"/api/v1/invoices/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertProblemDetailsAsync(response, StatusCodes.Status404NotFound);
    }
}
