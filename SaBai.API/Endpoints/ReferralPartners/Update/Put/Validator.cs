using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.ReferralPartners.Update.Put;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}
