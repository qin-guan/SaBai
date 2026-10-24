using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Puberty.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.VitalsId).GreaterThan(0);
    }
}
