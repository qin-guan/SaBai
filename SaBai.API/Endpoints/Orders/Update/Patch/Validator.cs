using FastEndpoints;
using FluentValidation;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.DosageInstructions).NotEmpty().When(x => x.DosageInstructions is not null);
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity is not null);
        RuleFor(x => x.MedicationActiveIngredientId)
            .NotEqual(Guid.Empty)
            .When(x => x.MedicationActiveIngredientId is not null);
        RuleFor(x => x)
            .Must(x => x.Quantity is null && x.MedicationActiveIngredientId is null)
            .When(x => x.Status == OrderStatus.Cancelled)
            .WithName("Quantity")
            .WithMessage("A cancelled order's quantity or ingredient cannot be changed.");
        RuleFor(x => x.Allocations)
            .Null()
            .When(x => x.Status == OrderStatus.Cancelled)
            .WithMessage("Stock batches cannot be chosen for an order that is being cancelled.");
        this.AllocationChoices(x => x.Allocations);
    }
}
