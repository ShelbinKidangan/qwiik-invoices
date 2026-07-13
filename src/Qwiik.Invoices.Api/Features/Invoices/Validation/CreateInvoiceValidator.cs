using FluentValidation;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Api.Features.Invoices.Validation;

// Fast-fail shape validation. Mirrors the domain invariants on purpose: a clean
// field-level 400 before construction, with the domain constructor still the last guard.
public sealed class CreateInvoiceValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .WithMessage("'Due Date' must be on or after 'Issue Date'.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000);

        RuleFor(x => x.LineItems)
            .NotEmpty()
            .WithMessage("An invoice must have at least one line item.");

        RuleForEach(x => x.LineItems)
            .SetValidator(new CreateLineItemValidator());
    }
}

public sealed class CreateLineItemValidator : AbstractValidator<CreateLineItemRequest>
{
    public CreateLineItemValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.TaxRate)
            .GreaterThanOrEqualTo(0);
    }
}
