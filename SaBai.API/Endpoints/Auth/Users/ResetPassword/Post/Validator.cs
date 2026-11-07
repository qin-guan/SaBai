using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Auth.Users.ResetPassword.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
