using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.MedicationStock.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.MedicationBrandId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Expiry).NotEqual(default(DateTimeOffset));
        RuleFor(x => x.Location).NotEmpty().MaximumLength(255);
        RuleFor(x => x.StockStatus).IsInEnum();
    }
}
