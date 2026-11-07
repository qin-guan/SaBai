using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Auth.LoginTwoFactor.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(16);
    }
}
