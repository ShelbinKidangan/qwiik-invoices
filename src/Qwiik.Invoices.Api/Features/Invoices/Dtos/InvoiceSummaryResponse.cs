namespace Qwiik.Invoices.Api.Features.Invoices.Dtos;

// Outstanding = Sent + Overdue totals; Paid = Paid total.
public sealed record InvoiceSummaryResponse(
    IReadOnlyDictionary<string, int> CountsByStatus,
    decimal TotalOutstanding,
    decimal TotalPaid,
    int OverdueCount,
    decimal OverdueAmount);
