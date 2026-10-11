using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Auth.Users.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Username).MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(255).When(x => x.Email is not null);
        RuleFor(x => x.Nickname).MaximumLength(255);
        RuleFor(x => x.Password).MinimumLength(8).When(x => x.Password is not null);
    }
}
