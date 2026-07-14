using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;
using Qwiik.Invoices.Tests.TestSupport;

namespace Qwiik.Invoices.Tests.Domain;

/// <summary>
/// Money is server-computed from the line items and rounded to 2dp AwayFromZero.
/// The client cannot supply totals — see <see cref="CreateInvoiceRequest_ExposesNoTotalsField"/>.
/// </summary>
public class InvoiceTotalsTests
{
    [Fact]
    public void RecalculateTotals_WithMixedTaxRates_ComputesRoundedTotals()
    {
        var a = InvoiceFactory.LineItem("A", quantity: 2m, unitPrice: 10.00m, taxRate: 5m);   // 20.00 + 1.00
        var b = InvoiceFactory.LineItem("B", quantity: 3m, unitPrice: 33.33m, taxRate: 10m);  // 99.99 + 9.999
        var c = InvoiceFactory.LineItem("C", quantity: 1m, unitPrice: 5.555m, taxRate: 20m);  // 5.555 + 1.111

        var invoice = InvoiceFactory.DraftInvoice(a, b, c);

        invoice.Subtotal.Should().Be(125.55m); // 125.545 rounded away from zero
        invoice.TaxTotal.Should().Be(12.11m);  // 12.110
        invoice.Total.Should().Be(137.66m);    // Subtotal + TaxTotal
    }

    [Fact]
    public void LineTotal_WithTaxRate_RoundsEachLineAwayFromZero()
    {
        var a = InvoiceFactory.LineItem("A", quantity: 2m, unitPrice: 10.00m, taxRate: 5m);
        var b = InvoiceFactory.LineItem("B", quantity: 3m, unitPrice: 33.33m, taxRate: 10m);
        var c = InvoiceFactory.LineItem("C", quantity: 1m, unitPrice: 5.555m, taxRate: 20m);

        a.LineTotal.Should().Be(21.00m);   // 20.00 * 1.05
        b.LineTotal.Should().Be(109.99m);  // 109.989 -> away from zero
        c.LineTotal.Should().Be(6.67m);    // 6.666  -> away from zero
    }

    [Fact]
    public void RecalculateTotals_WithFractionalCentRounding_TotalEqualsSumOfLineTotals()
    {
        // Regression: 1.11 * 1.05 = 1.1655 -> each line rounds to 1.17. Summing the raw
        // amounts and rounding once (the old approach) gave Total = 2.33 while the line
        // column showed 2.34 — a one-cent drift. Rounding each line's net and tax first
        // makes Subtotal + TaxTotal = Total = the sum of the line totals, exactly.
        var invoice = InvoiceFactory.DraftInvoice(
            InvoiceFactory.LineItem("A", quantity: 1m, unitPrice: 1.11m, taxRate: 5m),
            InvoiceFactory.LineItem("B", quantity: 1m, unitPrice: 1.11m, taxRate: 5m));

        var sumOfLineTotals = invoice.LineItems.Sum(li => li.LineTotal);

        invoice.Total.Should().Be(2.34m);
        invoice.Total.Should().Be(sumOfLineTotals);
        invoice.Subtotal.Should().Be(invoice.LineItems.Sum(li => li.NetAmount));
        invoice.TaxTotal.Should().Be(invoice.LineItems.Sum(li => li.TaxAmount));
    }

    [Fact]
    public void RecalculateTotals_WithZeroTax_LeavesTaxTotalZeroAndTotalEqualToSubtotal()
    {
        var invoice = InvoiceFactory.DraftInvoice(
            InvoiceFactory.LineItem("Flat", quantity: 4m, unitPrice: 12.50m, taxRate: 0m));

        invoice.Subtotal.Should().Be(50.00m);
        invoice.TaxTotal.Should().Be(0m);
        invoice.Total.Should().Be(50.00m);
    }

    [Fact]
    public void CreateInvoiceRequest_ExposesNoTotalsField()
    {
        // Guardrail: the client-facing request must not carry any money total — totals are
        // derived on the server so they cannot be forged via mass-assignment.
        var propertyNames = typeof(CreateInvoiceRequest)
            .GetProperties()
            .Select(p => p.Name);

        propertyNames.Should().NotContain(new[] { "Subtotal", "TaxTotal", "Total" });
    }
}
