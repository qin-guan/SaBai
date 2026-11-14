using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Referrals.RepayPn.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.MemberWhoPaidId)
            .NotEmpty();

        RuleFor(x => x.AmountUsd)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(99_999_999.99m)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .When(x => x.AmountUsd.HasValue);

        RuleFor(x => x.AmountRiels)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AmountRiels.HasValue);

        RuleFor(x => x)
            .Must(x => (x.AmountUsd ?? 0m) > 0m || (x.AmountRiels ?? 0) > 0)
            .WithMessage("Enter a repayment amount in USD or KHR.");

        RuleFor(x => x.ReimbursementNotes)
            .MaximumLength(500)
            .When(x => x.ReimbursementNotes is not null);
    }
}
