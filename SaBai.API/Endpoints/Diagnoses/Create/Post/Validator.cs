using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Diagnoses.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.ConsultId).NotEmpty();
        RuleFor(x => x.Details).NotEmpty();
    }
}
