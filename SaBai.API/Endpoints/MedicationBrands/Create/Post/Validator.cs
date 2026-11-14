using FastEndpoints;
using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.MedicationBrands.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).ClientId();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ActiveIngredientId).NotEmpty();
        RuleFor(x => x.Remarks).NotNull();
    }
}
