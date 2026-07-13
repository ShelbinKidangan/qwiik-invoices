using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;
using Qwiik.Invoices.Api.Infrastructure;

namespace Qwiik.Invoices.Api.Features.Invoices;

// Thin orchestration: drives the Invoice aggregate and persists via the DbContext.
// All business rules live in the domain, not here; not a repository (no generic CRUD).
public sealed class InvoiceService
{
    // A concurrent create can compute the same per-tenant number; the unique index on
    // (TenantId, InvoiceNumber) rejects the loser, and we recompute and retry.
    private const int MaxNumberRetries = 3;

    private readonly InvoiceDbContext _db;
    private readonly ITenantContext _tenant;

    public InvoiceService(InvoiceDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        var lineItems = request.LineItems
            .Select(li => new InvoiceLineItem(li.Description, li.Quantity, li.UnitPrice, li.TaxRate))
            .ToList();

        for (var attempt = 0; ; attempt++)
        {
            var number = await GenerateInvoiceNumberAsync(request.IssueDate.Year, ct);

            // TenantId is stamped by the DbContext from the tenant context, never bound
            // from the request — Guid.Empty here is a placeholder the stamp overwrites.
            var invoice = new Invoice(
                Guid.Empty,
                number,
                request.CustomerName,
                request.CustomerEmail,
                request.Currency,
                request.IssueDate,
                request.DueDate,
                lineItems,
                request.Notes);

            _db.Invoices.Add(invoice);

            try
            {
                await _db.SaveChangesAsync(ct);
                return invoice.ToResponse();
            }
            catch (DbUpdateException ex) when (IsInvoiceNumberCollision(ex) && attempt < MaxNumberRetries)
            {
                // Detach the failed graph and retry with a freshly computed number.
                _db.Entry(invoice).State = EntityState.Detached;
            }
        }
    }

    public async Task<InvoiceResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return invoice?.ToResponse();
    }

    public async Task<PagedResult<InvoiceListItemResponse>> ListAsync(PageQuery query, CancellationToken ct)
    {
        var filtered = _db.Invoices.AsNoTracking().AsQueryable();

        if (query.Status is { } status)
            filtered = filtered.Where(i => i.Status == status);
        if (query.IssueDateFrom is { } from)
            filtered = filtered.Where(i => i.IssueDate >= from);
        if (query.IssueDateTo is { } to)
            filtered = filtered.Where(i => i.IssueDate <= to);

        var totalCount = await filtered.CountAsync(ct);

        // Id is the tie-breaker so paging is stable when issue dates collide.
        var rows = await filtered
            .OrderByDescending(i => i.IssueDate)
            .ThenBy(i => i.Id)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.CustomerName,
                i.Status,
                i.Currency,
                i.IssueDate,
                i.DueDate,
                i.Total
            })
            .ToListAsync(ct);

        var items = rows
            .Select(r => new InvoiceListItemResponse(
                r.Id, r.InvoiceNumber, r.CustomerName, r.Status.ToString(),
                r.Currency, r.IssueDate, r.DueDate, r.Total))
            .ToList();

        return new PagedResult<InvoiceListItemResponse>(
            items, query.NormalizedPage, query.NormalizedPageSize, totalCount);
    }

    // Returns null if not found for this tenant; throws DomainException on an illegal
    // transition and DbUpdateConcurrencyException when the supplied RowVersion is stale.
    public async Task<InvoiceResponse?> ChangeStatusAsync(
        Guid id, InvoiceStatus status, byte[] rowVersion, CancellationToken ct)
    {
        var invoice = await _db.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null)
            return null;

        // Check the client's version, not the one we just read, so a concurrent update
        // between their read and this write is detected.
        _db.Entry(invoice).Property(i => i.RowVersion).OriginalValue = rowVersion;

        invoice.ChangeStatus(status);

        await _db.SaveChangesAsync(ct);
        return invoice.ToResponse();
    }

    public async Task<InvoiceSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        // One grouped round trip: GROUP BY Status -> (count, total) per status.
        var groups = await _db.Invoices
            .AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(x => x.Total) })
            .ToListAsync(ct);

        var byStatus = groups.ToDictionary(g => g.Status, g => g);

        decimal TotalFor(InvoiceStatus s) => byStatus.TryGetValue(s, out var g) ? g.Total : 0m;
        int CountFor(InvoiceStatus s) => byStatus.TryGetValue(s, out var g) ? g.Count : 0;

        // Every status is represented, including those with no invoices yet.
        var countsByStatus = Enum.GetValues<InvoiceStatus>()
            .ToDictionary(s => s.ToString(), CountFor);

        return new InvoiceSummaryResponse(
            CountsByStatus: countsByStatus,
            TotalOutstanding: TotalFor(InvoiceStatus.Sent) + TotalFor(InvoiceStatus.Overdue),
            TotalPaid: TotalFor(InvoiceStatus.Paid),
            OverdueCount: CountFor(InvoiceStatus.Overdue),
            OverdueAmount: TotalFor(InvoiceStatus.Overdue));
    }

    // Next per-tenant number as INV-{year}-{seq:D5}. The query filter scopes the max
    // lookup to the tenant; zero-padding keeps the string sort aligned with the sequence.
    private async Task<string> GenerateInvoiceNumberAsync(int year, CancellationToken ct)
    {
        var prefix = $"INV-{year}-";

        var last = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (last is not null &&
            int.TryParse(last.AsSpan(prefix.Length), out var lastSeq))
        {
            next = lastSeq + 1;
        }

        return $"{prefix}{next:D5}";
    }

    private static bool IsInvoiceNumberCollision(DbUpdateException ex) =>
        ex.InnerException is SqlException sql && sql.Number is 2601 or 2627;
}
