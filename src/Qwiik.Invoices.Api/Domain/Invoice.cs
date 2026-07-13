namespace Qwiik.Invoices.Api.Domain;

/// <summary>
/// Invoice aggregate root. Owns its line items, enforces status transitions,
/// and computes its own money totals. Totals and status are never assigned from
/// outside the domain — they are derived here so they cannot be tampered with.
/// </summary>
public sealed class Invoice
{
    private readonly List<InvoiceLineItem> _lineItems = new();

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string InvoiceNumber { get; private set; }
    public string CustomerName { get; private set; }
    public string CustomerEmail { get; private set; }
    public InvoiceStatus Status { get; private set; }

    public string Currency { get; private set; }

    public DateOnly IssueDate { get; private set; }
    public DateOnly DueDate { get; private set; }

    public decimal Subtotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal Total { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    // Required by EF Core to materialize the entity. The public constructor above
    // remains the only way for application code to create an invoice, so all
    // domain invariants are still enforced on construction.
    private Invoice()
    {
        InvoiceNumber = null!;
        CustomerName = null!;
        CustomerEmail = null!;
        Currency = null!;
    }

    public Invoice(
        Guid tenantId,
        string invoiceNumber,
        string customerName,
        string customerEmail,
        string currency,
        DateOnly issueDate,
        DateOnly dueDate,
        IEnumerable<InvoiceLineItem> lineItems,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new DomainException("Invoice number is required.");
        if (string.IsNullOrWhiteSpace(customerName))
            throw new DomainException("Customer name is required.");
        if (string.IsNullOrWhiteSpace(customerEmail))
            throw new DomainException("Customer email is required.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("Currency must be a 3-letter ISO code.");
        if (dueDate < issueDate)
            throw new DomainException("Due date cannot be earlier than the issue date.");

        var items = lineItems?.ToList() ?? new List<InvoiceLineItem>();
        if (items.Count == 0)
            throw new DomainException("An invoice must have at least one line item.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        InvoiceNumber = invoiceNumber;
        CustomerName = customerName;
        CustomerEmail = customerEmail;
        Currency = currency;
        IssueDate = issueDate;
        DueDate = dueDate;
        Notes = notes;
        Status = InvoiceStatus.Draft;

        var now = DateTime.UtcNow;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;

        _lineItems.AddRange(items);
        RecalculateTotals();
    }

    /// <summary>
    /// Applies a status transition, enforcing the allowed state machine:
    /// Draft → Sent | Cancelled; Sent → Paid | Overdue | Cancelled;
    /// Overdue → Paid; Paid and Cancelled are terminal.
    /// </summary>
    public void ChangeStatus(InvoiceStatus newStatus)
    {
        var allowed = Status switch
        {
            InvoiceStatus.Draft => newStatus is InvoiceStatus.Sent or InvoiceStatus.Cancelled,
            InvoiceStatus.Sent => newStatus is InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Cancelled,
            InvoiceStatus.Overdue => newStatus is InvoiceStatus.Paid,
            _ => false
        };

        if (!allowed)
            throw new DomainException($"Cannot change invoice status from {Status} to {newStatus}.");

        Status = newStatus;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Recomputes Subtotal, TaxTotal and Total from the line items. Server-computed
    /// only — money is never accepted from outside the domain.
    /// </summary>
    private void RecalculateTotals()
    {
        decimal subtotal = 0m;
        decimal taxTotal = 0m;

        foreach (var item in _lineItems)
        {
            subtotal += item.Quantity * item.UnitPrice;
            taxTotal += item.Quantity * item.UnitPrice * item.TaxRate / 100;
        }

        Subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        TaxTotal = Math.Round(taxTotal, 2, MidpointRounding.AwayFromZero);
        Total = Subtotal + TaxTotal;
    }
}
