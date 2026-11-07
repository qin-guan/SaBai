using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Auth.Account.Password.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}
