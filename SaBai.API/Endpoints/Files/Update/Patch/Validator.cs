using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Files.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FileName).MaximumLength(255);
    }
}
