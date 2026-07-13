using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Tests.Domain;

/// <summary>
/// Construction-time invariants on the <see cref="Invoice"/> aggregate. Each invalid
/// input is rejected with a <see cref="DomainException"/> before an invoice exists.
/// </summary>
public class InvoiceInvariantsTests
{
    private static readonly DateOnly Issue = new(2026, 1, 1);
    private static readonly DateOnly Due = new(2026, 1, 31);

    private static InvoiceLineItem ValidLineItem() => new("Consulting", 1m, 100m, 0m);

    private static Invoice Build(
        string invoiceNumber = "INV-0001",
        string customerName = "Acme Corp",
        string customerEmail = "billing@acme.test",
        string currency = "USD",
        DateOnly? issueDate = null,
        DateOnly? dueDate = null,
        IEnumerable<InvoiceLineItem>? lineItems = null) =>
        new(
            Guid.NewGuid(),
            invoiceNumber,
            customerName,
            customerEmail,
            currency,
            issueDate ?? Issue,
            dueDate ?? Due,
            lineItems ?? new[] { ValidLineItem() });

    [Fact]
    public void Construct_WithValidInput_Succeeds()
    {
        var act = () => Build();
        act.Should().NotThrow();
    }

    [Fact]
    public void Construct_WithDueDateBeforeIssueDate_ThrowsDomainException()
    {
        var act = () => Build(issueDate: Due, dueDate: Issue);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Construct_WithNoLineItems_ThrowsDomainException()
    {
        var act = () => Build(lineItems: Array.Empty<InvoiceLineItem>());
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Construct_WithCurrencyNotThreeLetters_ThrowsDomainException(string currency)
    {
        var act = () => Build(currency: currency);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Construct_WithBlankInvoiceNumber_ThrowsDomainException(string invoiceNumber)
    {
        var act = () => Build(invoiceNumber: invoiceNumber);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Construct_WithBlankCustomerName_ThrowsDomainException(string customerName)
    {
        var act = () => Build(customerName: customerName);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Construct_WithBlankCustomerEmail_ThrowsDomainException(string customerEmail)
    {
        var act = () => Build(customerEmail: customerEmail);
        act.Should().Throw<DomainException>();
    }
}
