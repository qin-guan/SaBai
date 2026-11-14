using FastEndpoints;
using FluentValidation;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Auth.Users.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Nickname).MaximumLength(255);
        RuleForEach(x => x.Roles)
            .Must(StaffRoles.IsKnown)
            .WithMessage(x => $"Roles must be one of: {string.Join(", ", StaffRoles.All)}.");
        RuleForEach(x => x.Tags)
            .Must(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length <= TagSync.MaxTagLength)
            .WithMessage($"Tags must be {TagSync.MaxTagLength} characters or fewer.");
    }
}
