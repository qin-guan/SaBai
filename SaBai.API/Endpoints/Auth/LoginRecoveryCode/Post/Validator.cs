using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Auth.LoginRecoveryCode.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.RecoveryCode).NotEmpty().MaximumLength(32);
    }
}
