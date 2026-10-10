using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Broadcasts.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Severity).IsInEnum();
    }
}
