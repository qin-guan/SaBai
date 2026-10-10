using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Puberty.Get;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
