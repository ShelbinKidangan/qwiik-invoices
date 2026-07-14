using System.Net.Http.Json;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Tests.Integration;

/// <summary>
/// The list endpoint pages, filters, and clamps its inputs server-side; every assertion
/// here rides the real EF query path against SQL Server.
/// </summary>
public sealed class InvoiceListTests : IntegrationTestBase
{
    public InvoiceListTests(SqlServerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task List_SecondPage_ReturnsRequestedPage()
    {
        var tenantId = Guid.NewGuid();
        await SeedInvoicesAsync(tenantId, 3);

        using var client = Factory.ClientForTenant(tenantId);
        var page = await client.GetFromJsonAsync<PagedResult<InvoiceListItemResponse>>(
            "/api/v1/invoices?page=2&pageSize=1", Json);

        page.Should().NotBeNull();
        page!.Page.Should().Be(2);
        page.PageSize.Should().Be(1);
        page.Items.Should().ContainSingle();
        page.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task List_FilterByStatus_ReturnsOnlyMatching()
    {
        var tenantId = Guid.NewGuid();
        var invoices = await SeedInvoicesAsync(tenantId, 3);

        // Move exactly one invoice out of Draft into Sent.
        var sent = invoices[0];
        await ChangeStatusAsync(tenantId, sent.Id, InvoiceStatus.Sent, sent.RowVersion);

        using var client = Factory.ClientForTenant(tenantId);
        var page = await client.GetFromJsonAsync<PagedResult<InvoiceListItemResponse>>(
            "/api/v1/invoices?status=Sent", Json);

        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle()
            .Which.Id.Should().Be(sent.Id);
        page.Items.Should().OnlyContain(i => i.Status == nameof(InvoiceStatus.Sent));
    }

    [Fact]
    public async Task List_FilterByIssueDateRange_ReturnsOnlyInRange()
    {
        var tenantId = Guid.NewGuid();

        await SeedWithIssueDateAsync(tenantId, new DateOnly(2026, 1, 15));
        var inRange = await SeedWithIssueDateAsync(tenantId, new DateOnly(2026, 2, 15));
        await SeedWithIssueDateAsync(tenantId, new DateOnly(2026, 3, 15));

        using var client = Factory.ClientForTenant(tenantId);
        var page = await client.GetFromJsonAsync<PagedResult<InvoiceListItemResponse>>(
            "/api/v1/invoices?issueDateFrom=2026-02-01&issueDateTo=2026-02-28", Json);

        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle()
            .Which.Id.Should().Be(inRange.Id);
    }

    [Fact]
    public async Task List_PageSizeAboveMax_IsCappedAt100()
    {
        var tenantId = Guid.NewGuid();
        using var client = Factory.ClientForTenant(tenantId);

        // PagedResult echoes the server-normalized page size, so no rows need seeding.
        var page = await client.GetFromJsonAsync<PagedResult<InvoiceListItemResponse>>(
            "/api/v1/invoices?pageSize=200", Json);

        page.Should().NotBeNull();
        page!.PageSize.Should().Be(100);
    }

    private Task<InvoiceResponse> SeedWithIssueDateAsync(Guid tenantId, DateOnly issueDate) =>
        CreateInvoiceAsync(tenantId, ValidCreateRequest() with
        {
            IssueDate = issueDate,
            DueDate = issueDate.AddDays(30),
        });
}
