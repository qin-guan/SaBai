using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Referrals.UpdateReimbursement.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.ReimbursementNotes)
            .MaximumLength(500)
            .When(x => x.ReimbursementNotes is not null);

        RuleFor(x => x.ReimbursedUsd)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(99_999_999.99m)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .When(x => x.ReimbursedUsd.HasValue);

        RuleFor(x => x.ReimbursedRiels)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ReimbursedRiels.HasValue);
    }
}
