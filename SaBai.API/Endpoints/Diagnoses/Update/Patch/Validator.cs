using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Diagnoses.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Details).NotEmpty().When(x => x.Details is not null);
    }
}
