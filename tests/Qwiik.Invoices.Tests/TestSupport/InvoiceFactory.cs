using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Tests.TestSupport;

/// <summary>
/// Builds valid domain objects for unit tests. Keeps the individual tests focused on
/// the one rule under test rather than on ceremony.
/// </summary>
internal static class InvoiceFactory
{
    private static readonly DateOnly IssueDate = new(2026, 1, 1);
    private static readonly DateOnly DueDate = new(2026, 1, 31);

    public static InvoiceLineItem LineItem(
        string description = "Consulting",
        decimal quantity = 1m,
        decimal unitPrice = 100m,
        decimal taxRate = 0m) =>
        new(description, quantity, unitPrice, taxRate);

    /// <summary>A valid, freshly-created Draft invoice with a single line item.</summary>
    public static Invoice DraftInvoice(params InvoiceLineItem[] lineItems) =>
        new(
            tenantId: Guid.NewGuid(),
            invoiceNumber: "INV-0001",
            customerName: "Acme Corp",
            customerEmail: "billing@acme.test",
            currency: "USD",
            issueDate: IssueDate,
            dueDate: DueDate,
            lineItems: lineItems.Length == 0 ? new[] { LineItem() } : lineItems);

    /// <summary>
    /// Produces an invoice sitting in <paramref name="status"/>, reached only via legal
    /// transitions so the starting state is itself valid.
    /// </summary>
    public static Invoice InvoiceInStatus(InvoiceStatus status)
    {
        var invoice = DraftInvoice();
        switch (status)
        {
            case InvoiceStatus.Draft:
                break;
            case InvoiceStatus.Sent:
                invoice.ChangeStatus(InvoiceStatus.Sent);
                break;
            case InvoiceStatus.Paid:
                invoice.ChangeStatus(InvoiceStatus.Sent);
                invoice.ChangeStatus(InvoiceStatus.Paid);
                break;
            case InvoiceStatus.Overdue:
                invoice.ChangeStatus(InvoiceStatus.Sent);
                invoice.ChangeStatus(InvoiceStatus.Overdue);
                break;
            case InvoiceStatus.Cancelled:
                invoice.ChangeStatus(InvoiceStatus.Cancelled);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return invoice;
    }
}
