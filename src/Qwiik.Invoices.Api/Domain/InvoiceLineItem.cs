namespace Qwiik.Invoices.Api.Domain;

/// <summary>
/// A single billable line on an invoice. Quantity, price and tax are validated
/// on construction; the total is always computed, never supplied.
/// </summary>
public sealed class InvoiceLineItem
{
    public Guid Id { get; private set; }
    public string Description { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    public decimal TaxRate { get; private set; }

    /// <summary>Line amount including tax, rounded to 2 decimal places.</summary>
    public decimal LineTotal =>
        Math.Round(Quantity * UnitPrice * (1 + TaxRate / 100), 2, MidpointRounding.AwayFromZero);

    public InvoiceLineItem(string description, decimal quantity, decimal unitPrice, decimal taxRate)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Line item description is required.");
        if (quantity <= 0)
            throw new DomainException("Line item quantity must be greater than zero.");
        if (unitPrice < 0)
            throw new DomainException("Line item unit price cannot be negative.");
        if (taxRate < 0)
            throw new DomainException("Line item tax rate cannot be negative.");

        Id = Guid.NewGuid();
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
    }
}
