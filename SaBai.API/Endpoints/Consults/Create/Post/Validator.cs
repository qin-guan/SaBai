using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Consults.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.VisitId).GreaterThan(0);
        RuleFor(x => x.Diagnoses).NotEmpty();
        RuleForEach(x => x.Diagnoses)
            .ChildRules(diagnosis =>
            {
                diagnosis.RuleFor(x => x.Details).NotEmpty();
            });
        RuleForEach(x => x.Orders)
            .ChildRules(order =>
            {
                order.RuleFor(x => x.MedicationBrandId).GreaterThan(0);
                order.RuleFor(x => x.Quantity).GreaterThan(0);
            });
    }
}
