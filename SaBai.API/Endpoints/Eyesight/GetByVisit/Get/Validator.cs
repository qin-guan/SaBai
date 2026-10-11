using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Eyesight.GetByVisit.Get;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.VisitId).GreaterThan(0);
    }
}
