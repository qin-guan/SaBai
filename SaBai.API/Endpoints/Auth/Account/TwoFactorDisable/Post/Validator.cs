using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Auth.Account.TwoFactorDisable.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Password).NotEmpty();
    }
}
