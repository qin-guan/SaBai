using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Referrals.UpdateOutcome.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ReferralOutcome).NotNull().MaximumLength(2000);
    }
}
