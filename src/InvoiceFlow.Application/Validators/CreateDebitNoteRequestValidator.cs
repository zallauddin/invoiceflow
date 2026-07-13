using FluentValidation;
using InvoiceFlow.Core.DTOs.Documents;

namespace InvoiceFlow.Application.Validators;

/// <summary>Validates the <see cref="CreateDebitNoteRequest"/> payload.</summary>
public sealed class CreateDebitNoteRequestValidator : AbstractValidator<CreateDebitNoteRequest>
{
    public CreateDebitNoteRequestValidator()
    {
        RuleFor(x => x.DocumentNumber)
            .NotEmpty().WithMessage("DocumentNumber is required.")
            .MaximumLength(50);

        RuleFor(x => x.IssuerName)
            .NotEmpty().WithMessage("IssuerName is required.")
            .MaximumLength(200);

        RuleFor(x => x.IssuerEmail)
            .EmailAddress().WithMessage("IssuerEmail is not a valid email address.");

        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("RecipientName is required.")
            .MaximumLength(200);

        RuleFor(x => x.RecipientEmail)
            .EmailAddress().WithMessage("RecipientEmail is not a valid email address.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency must be a 3-letter ISO code.")
            .MaximumLength(3);

        RuleFor(x => x.Subtotal)
            .GreaterThanOrEqualTo(0).WithMessage("Subtotal must be greater than or equal to 0.");

        RuleFor(x => x.TaxAmount)
            .GreaterThanOrEqualTo(0).WithMessage("TaxAmount must be greater than or equal to 0.");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("TotalAmount must be greater than or equal to 0.");

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("DiscountAmount must be greater than or equal to 0.")
            .When(x => x.DiscountAmount.HasValue);

        RuleFor(x => x.ShippingAmount)
            .GreaterThanOrEqualTo(0).WithMessage("ShippingAmount must be greater than or equal to 0.")
            .When(x => x.ShippingAmount.HasValue);

        RuleFor(x => x.DocumentDate)
            .LessThan(DateTime.UtcNow.AddYears(1)).WithMessage("DocumentDate must be in the past or present.")
            .When(x => x.DocumentDate.HasValue);

        RuleFor(x => x.DueDate)
            .GreaterThan(x => x.DocumentDate).WithMessage("DueDate must be after DocumentDate.")
            .When(x => x.DueDate.HasValue && x.DocumentDate.HasValue);

        RuleFor(x => x.CountryCode)
            .MaximumLength(2);

        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(100);

        RuleFor(x => x.Notes)
            .MaximumLength(2000);

        RuleFor(x => x.IssuerTaxId)
            .MaximumLength(50);

        RuleFor(x => x.IssuerAddress)
            .MaximumLength(2000);

        RuleFor(x => x.RecipientTaxId)
            .MaximumLength(50);

        RuleFor(x => x.RecipientAddress)
            .MaximumLength(2000);

        RuleFor(x => x.Reason)
            .MaximumLength(500);
    }
}
