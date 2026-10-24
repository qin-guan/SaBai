using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Referrals.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.ConsultId).GreaterThan(0);
        RuleFor(x => x.ReferredFor).NotEmpty();
    }
}
