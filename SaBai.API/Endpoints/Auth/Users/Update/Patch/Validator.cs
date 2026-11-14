using FastEndpoints;
using FluentValidation;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Auth.Users.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(150).When(x => x.UserName is not null);
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Nickname).NotEmpty().MaximumLength(255).When(x => x.Nickname is not null);
        RuleFor(x => x.Roles)
            .Must(x => x is null || x.Count > 0)
            .WithMessage("A user needs at least one role.");
        RuleForEach(x => x.Roles)
            .Must(StaffRoles.IsKnown)
            .WithMessage(x => $"Roles must be one of: {string.Join(", ", StaffRoles.All)}.");
        RuleForEach(x => x.Tags)
            .Must(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length <= TagSync.MaxTagLength)
            .WithMessage($"Tags must be {TagSync.MaxTagLength} characters or fewer.");
    }
}
