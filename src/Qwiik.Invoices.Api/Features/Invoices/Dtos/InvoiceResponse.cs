namespace Qwiik.Invoices.Api.Features.Invoices.Dtos;

public sealed record InvoiceResponse(
    Guid Id,
    string InvoiceNumber,
    string CustomerName,
    string CustomerEmail,
    string Status,
    string Currency,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Subtotal,
    decimal TaxTotal,
    decimal Total,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string RowVersion,
    IReadOnlyList<LineItemResponse> LineItems);

public sealed record LineItemResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal LineTotal);
