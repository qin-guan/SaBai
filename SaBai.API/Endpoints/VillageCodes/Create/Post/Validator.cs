using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.VillageCodes.Create.Post;

public sealed class Validator : Validator<Request>
{
    public const string ColorHexPattern = "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$";

    public Validator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ColorHex).NotEmpty().Matches(ColorHexPattern);
    }
}
