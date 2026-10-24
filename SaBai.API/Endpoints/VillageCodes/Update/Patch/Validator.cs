using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.VillageCodes.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(255).When(x => x.Code is not null);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).When(x => x.Name is not null);
        RuleFor(x => x.ColorHex)
            .Matches("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")
            .When(x => x.ColorHex is not null);
    }
}
