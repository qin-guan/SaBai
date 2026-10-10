using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.ChildVitals.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.VitalsId).GreaterThan(0);
    }
}
