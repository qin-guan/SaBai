using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.MedicationBrands.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ActiveIngredientId).GreaterThan(0);
        RuleFor(x => x.Remarks).NotNull();
    }
}
