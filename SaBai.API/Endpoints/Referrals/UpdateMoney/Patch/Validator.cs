using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Referrals.UpdateMoney.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.AmountPaidUsd)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(99_999_999.99m)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .When(x => x.AmountPaidUsd.HasValue);

        RuleFor(x => x.AmountPaidRiels)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AmountPaidRiels.HasValue);
    }
}
