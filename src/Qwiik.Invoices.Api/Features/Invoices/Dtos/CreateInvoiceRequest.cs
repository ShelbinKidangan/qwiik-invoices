namespace Qwiik.Invoices.Api.Features.Invoices.Dtos;

// No TenantId, Id, Status, InvoiceNumber, or totals here — all server-derived, so they
// cannot be forged via mass-assignment.
public sealed record CreateInvoiceRequest(
    string CustomerName,
    string CustomerEmail,
    string Currency,
    DateOnly IssueDate,
    DateOnly DueDate,
    IReadOnlyList<CreateLineItemRequest> LineItems,
    string? Notes = null);

public sealed record CreateLineItemRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate);
