using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Vision.Get;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.VisitId).GreaterThan(0);
    }
}
