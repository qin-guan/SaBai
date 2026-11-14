using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.PnLedger.Repay.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.AmountUsd)
            .GreaterThanOrEqualTo(0m)
            .When(x => x.AmountUsd.HasValue);
        RuleFor(x => x.AmountRiels)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AmountRiels.HasValue);
        RuleFor(x => x)
            .Must(x => (x.AmountUsd ?? 0m) > 0m || (x.AmountRiels ?? 0) > 0)
            .WithMessage("Enter a positive USD or KHR repayment amount.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
