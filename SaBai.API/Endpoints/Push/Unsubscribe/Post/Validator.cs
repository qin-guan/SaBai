using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Push.Unsubscribe.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Endpoint).NotEmpty();
    }
}
