using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.MedicationBrands.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).When(x => x.Name is not null);
        RuleFor(x => x.ActiveIngredientId).GreaterThan(0);
        RuleFor(x => x.Remarks).NotNull();
    }
}
