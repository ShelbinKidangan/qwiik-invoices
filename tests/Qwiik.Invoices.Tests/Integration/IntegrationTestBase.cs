using System.Net.Http.Json;
using System.Text.Json;
using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// Base for integration test classes: owns the shared factory and resets the database
/// before each test so every test starts from a clean, isolated state.
/// </summary>
[Collection("integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    // Web defaults match the API's camelCase JSON contract for reading responses back.
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SqlServerFixture _fixture;
    protected readonly InvoiceApiFactory Factory;

    protected IntegrationTestBase(SqlServerFixture fixture)
    {
        _fixture = fixture;
        Factory = new InvoiceApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }

    // Asserts a response carries an RFC 7807 ProblemDetails body with the given status.
    // Note: the controller's Problem()/ValidationProblem() and the tenant middleware serve
    // the body as "application/json" (only the GlobalExceptionHandler path uses
    // "application/problem+json"), so we assert the body shape, not the media type.
    protected static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response, int expectedStatus)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Contain("json");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        doc.RootElement.TryGetProperty("status", out var status).Should().BeTrue(
            "a ProblemDetails body carries a numeric status");
        status.GetInt32().Should().Be(expectedStatus);
    }

    // A minimal valid create request; the invoice number and totals are server-derived.
    protected static CreateInvoiceRequest ValidCreateRequest() => new(
        CustomerName: "Acme Corp",
        CustomerEmail: "billing@acme.test",
        Currency: "USD",
        IssueDate: new DateOnly(2026, 1, 1),
        DueDate: new DateOnly(2026, 1, 31),
        LineItems: new[]
        {
            new CreateLineItemRequest("Consulting", 2m, 100.00m, 10m),
        },
        Notes: null);

    // Creates an invoice for the given tenant with the default valid request.
    protected Task<InvoiceResponse> CreateInvoiceAsync(Guid tenantId) =>
        CreateInvoiceAsync(tenantId, ValidCreateRequest());

    // Creates an invoice for the given tenant from a caller-supplied request.
    protected async Task<InvoiceResponse> CreateInvoiceAsync(Guid tenantId, CreateInvoiceRequest request)
    {
        using var client = Factory.ClientForTenant(tenantId);
        var response = await client.PostAsJsonAsync("/api/v1/invoices", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;
    }

    // Seeds <paramref name="count"/> draft invoices for the tenant, in creation order.
    protected async Task<IReadOnlyList<InvoiceResponse>> SeedInvoicesAsync(Guid tenantId, int count)
    {
        var created = new List<InvoiceResponse>(count);
        for (var i = 0; i < count; i++)
            created.Add(await CreateInvoiceAsync(tenantId));
        return created;
    }

    // Patches an invoice to a new status and returns the updated response (expects success).
    protected async Task<InvoiceResponse> ChangeStatusAsync(
        Guid tenantId, Guid id, InvoiceStatus status, string rowVersion)
    {
        using var client = Factory.ClientForTenant(tenantId);
        var response = await client.PatchAsJsonAsync(
            $"/api/v1/invoices/{id}/status", new UpdateStatusRequest(status, rowVersion));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;
    }
}
