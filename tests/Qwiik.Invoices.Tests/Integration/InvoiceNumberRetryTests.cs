using System.Net;
using System.Net.Http.Json;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// Per-tenant invoice numbers are computed as MAX+1 and guarded by the unique index on
/// (TenantId, InvoiceNumber): concurrent creates can collide, so the loser recomputes and
/// retries. This drives that race on real SQL Server and proves every create still ends up
/// with a distinct number and its own line items.
/// </summary>
public sealed class InvoiceNumberRetryTests : IntegrationTestBase
{
    // N is capped at 3 to stay within the service's retry budget: MaxNumberRetries is 3,
    // and the k-th concurrent create can face up to k-1 index collisions before it wins.
    // With N=3 the worst case is 2 retries — safely inside the budget — so the race stays
    // deterministic and never surfaces as a flaky 500. Raising N past 4 could exhaust the
    // retries and fail the create, which is not what this test is about.
    private const int ConcurrentCreates = 3;

    public InvoiceNumberRetryTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Create_ConcurrentForSameTenant_AllSucceedWithDistinctNumbers()
    {
        var tenantId = Guid.NewGuid();
        var request = ValidCreateRequest() with { IssueDate = new DateOnly(2026, 1, 1) };
        var year = request.IssueDate.Year;

        // Fire all creates at once so they contend for the same next number on one tenant.
        // Each task uses its own client (HttpClient default headers are set once per client).
        var tasks = Enumerable.Range(0, ConcurrentCreates)
            .Select(async _ =>
            {
                using var client = Factory.ClientForTenant(tenantId);
                return await client.PostAsJsonAsync("/api/v1/invoices", request);
            })
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Every concurrent create wins a slot — no collision escapes the retry loop.
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);

        var created = new List<InvoiceResponse>(ConcurrentCreates);
        foreach (var response in responses)
            created.Add((await response.Content.ReadFromJsonAsync<InvoiceResponse>(Json))!);

        // Numbers are distinct and each matches the INV-{year}-{seq:D5} contract.
        var numbers = created.Select(c => c.InvoiceNumber).ToList();
        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(n => n.StartsWith($"INV-{year}-"));
        numbers.Should().BeEquivalentTo(
            Enumerable.Range(1, ConcurrentCreates).Select(seq => $"INV-{year}-{seq:D5}"));

        // Each invoice carries its OWN line items — guards the fix where a retry reused the
        // previous attempt's already-tracked line-item graph across creates.
        var lineItemIds = created
            .SelectMany(c => c.LineItems.Select(li => li.Id))
            .ToList();
        created.Should().OnlyContain(c => c.LineItems.Count == 1);
        lineItemIds.Should().OnlyHaveUniqueItems(
            "a retried create must build fresh line items, not reuse another invoice's");
    }
}
