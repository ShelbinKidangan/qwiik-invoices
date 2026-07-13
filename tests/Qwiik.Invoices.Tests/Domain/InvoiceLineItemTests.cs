using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Tests.Domain;

/// <summary>
/// Construction-time invariants on <see cref="InvoiceLineItem"/>.
/// </summary>
public class InvoiceLineItemTests
{
    private static InvoiceLineItem Build(
        string description = "Consulting",
        decimal quantity = 1m,
        decimal unitPrice = 100m,
        decimal taxRate = 0m) =>
        new(description, quantity, unitPrice, taxRate);

    [Fact]
    public void Construct_WithValidInput_Succeeds()
    {
        var act = () => Build();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Construct_WithBlankDescription_ThrowsDomainException(string description)
    {
        var act = () => Build(description: description);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Construct_WithNonPositiveQuantity_ThrowsDomainException(decimal quantity)
    {
        var act = () => Build(quantity: quantity);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Construct_WithNegativeUnitPrice_ThrowsDomainException()
    {
        var act = () => Build(unitPrice: -0.01m);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Construct_WithNegativeTaxRate_ThrowsDomainException()
    {
        var act = () => Build(taxRate: -1m);
        act.Should().Throw<DomainException>();
    }
}
