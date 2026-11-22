using FastEndpoints;
using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Orders.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).ClientId();
        RuleFor(x => x.ConsultId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.MedicationActiveIngredientId is { } i && i != Guid.Empty
                || x.MedicationBrandId is { } b && b != Guid.Empty)
            .WithName("MedicationActiveIngredientId")
            .WithMessage("An active ingredient (or a medication brand) is required.");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.DosageInstructions).NotEmpty();
    }
}
