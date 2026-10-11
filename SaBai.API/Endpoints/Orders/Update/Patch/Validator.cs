using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Orders.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).NotNull().IsInEnum();
        RuleFor(x => x.DosageInstructions).NotEmpty().When(x => x.DosageInstructions is not null);
    }
}
