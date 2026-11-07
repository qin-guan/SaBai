using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Auth.Account.TwoFactorEnable.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
    }
}
