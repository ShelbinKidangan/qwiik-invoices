using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Api.Features.Invoices;

// Hand-written mapping by design — no AutoMapper for a surface this small.
internal static class InvoiceMapping
{
    public static InvoiceResponse ToResponse(this Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.CustomerName,
        invoice.CustomerEmail,
        invoice.Status.ToString(),
        invoice.Currency,
        invoice.IssueDate,
        invoice.DueDate,
        invoice.Subtotal,
        invoice.TaxTotal,
        invoice.Total,
        invoice.Notes,
        invoice.CreatedAtUtc,
        invoice.UpdatedAtUtc,
        Convert.ToBase64String(invoice.RowVersion),
        invoice.LineItems.Select(ToLineItemResponse).ToList());

    private static LineItemResponse ToLineItemResponse(InvoiceLineItem item) => new(
        item.Id,
        item.Description,
        item.Quantity,
        item.UnitPrice,
        item.TaxRate,
        item.LineTotal);
}
