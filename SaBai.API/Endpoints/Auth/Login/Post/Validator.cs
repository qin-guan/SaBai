using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Auth.Login.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
