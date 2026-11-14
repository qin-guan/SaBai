using FastEndpoints;
using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.ChildVitals.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).ClientId();
        RuleFor(x => x.VitalsId).NotEmpty();
    }
}
