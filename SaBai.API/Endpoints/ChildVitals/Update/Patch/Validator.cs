using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.ChildVitals.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
