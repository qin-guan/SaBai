using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Eyesight.UpdateByVisit.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.VisitId).GreaterThan(0);
    }
}
