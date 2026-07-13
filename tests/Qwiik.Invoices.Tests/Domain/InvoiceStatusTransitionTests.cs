using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Tests.TestSupport;

namespace Qwiik.Invoices.Tests.Domain;

/// <summary>
/// Exercises the full 5x5 status matrix. The seven legal transitions succeed and move
/// the invoice; the other eighteen (self-transitions, and any move out of the terminal
/// Paid/Cancelled states) throw <see cref="DomainException"/>.
/// </summary>
public class InvoiceStatusTransitionTests
{
    // The seven transitions the state machine permits.
    private static readonly HashSet<(InvoiceStatus From, InvoiceStatus To)> Legal = new()
    {
        (InvoiceStatus.Draft, InvoiceStatus.Sent),
        (InvoiceStatus.Draft, InvoiceStatus.Cancelled),
        (InvoiceStatus.Sent, InvoiceStatus.Paid),
        (InvoiceStatus.Sent, InvoiceStatus.Overdue),
        (InvoiceStatus.Sent, InvoiceStatus.Cancelled),
        (InvoiceStatus.Overdue, InvoiceStatus.Paid),
    };

    public static IEnumerable<object[]> AllTransitions()
    {
        foreach (InvoiceStatus from in Enum.GetValues<InvoiceStatus>())
        foreach (InvoiceStatus to in Enum.GetValues<InvoiceStatus>())
            yield return new object[] { from, to, Legal.Contains((from, to)) };
    }

    [Theory]
    [MemberData(nameof(AllTransitions))]
    public void ChangeStatus_OverFullMatrix_AllowsOnlyLegalTransitions(
        InvoiceStatus from, InvoiceStatus to, bool isLegal)
    {
        var invoice = InvoiceFactory.InvoiceInStatus(from);

        if (isLegal)
        {
            invoice.ChangeStatus(to);
            invoice.Status.Should().Be(to);
        }
        else
        {
            var act = () => invoice.ChangeStatus(to);
            act.Should().Throw<DomainException>();
            invoice.Status.Should().Be(from, "a rejected transition must not mutate status");
        }
    }
}
