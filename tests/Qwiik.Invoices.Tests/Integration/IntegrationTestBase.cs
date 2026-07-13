using System.Net.Http.Json;
using System.Text.Json;
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

    // Creates an invoice for the given tenant and returns the deserialized response.
    protected async Task<InvoiceResponse> CreateInvoiceAsync(Guid tenantId)
    {
        using var client = Factory.ClientForTenant(tenantId);
        var response = await client.PostAsJsonAsync("/api/v1/invoices", ValidCreateRequest());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!;
    }
}
