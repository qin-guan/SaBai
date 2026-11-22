using FastEndpoints;
using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Consults.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).ClientId();
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.Diagnoses).NotEmpty();
        RuleForEach(x => x.Diagnoses)
            .ChildRules(diagnosis =>
            {
                diagnosis.RuleFor(x => x.Id).ClientId();
                diagnosis.RuleFor(x => x.Details).NotEmpty();
            });
        RuleForEach(x => x.Orders)
            .ChildRules(order =>
            {
                order.RuleFor(x => x.Id).ClientId();
                order
                    .RuleFor(x => x)
                    .Must(x => x.MedicationActiveIngredientId is { } i && i != Guid.Empty
                        || x.MedicationBrandId is { } b && b != Guid.Empty)
                    .WithName("MedicationActiveIngredientId")
                    .WithMessage("An active ingredient (or a medication brand) is required.");
                order.RuleFor(x => x.Quantity).GreaterThan(0);
            });
    }
}
