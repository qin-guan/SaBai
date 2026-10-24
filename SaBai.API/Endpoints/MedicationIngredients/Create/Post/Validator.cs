using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.MedicationIngredients.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.UnitOfMeasurement).NotEmpty().MaximumLength(255);
        RuleFor(x => x.FallBelow).GreaterThan(0);
    }
}
