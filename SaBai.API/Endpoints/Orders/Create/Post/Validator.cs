using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Orders.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.ConsultId).GreaterThan(0);
        RuleFor(x => x.MedicationBrandId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.DosageInstructions).NotEmpty();
    }
}
