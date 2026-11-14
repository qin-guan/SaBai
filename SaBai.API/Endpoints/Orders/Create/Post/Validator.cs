using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Orders.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.ConsultId).NotEmpty();
        RuleFor(x => x.MedicationBrandId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.DosageInstructions).NotEmpty();
    }
}
