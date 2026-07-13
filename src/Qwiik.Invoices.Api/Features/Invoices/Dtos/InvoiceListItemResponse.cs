namespace Qwiik.Invoices.Api.Features.Invoices.Dtos;

// Lean list row: omits line items so the list query never over-fetches the aggregate.
public sealed record InvoiceListItemResponse(
    Guid Id,
    string InvoiceNumber,
    string CustomerName,
    string Status,
    string Currency,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Total);
