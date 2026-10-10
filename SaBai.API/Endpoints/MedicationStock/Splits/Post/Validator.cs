using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.MedicationStock.Splits.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Splits).NotNull().Must(x => x is { Count: >= 2 and <= 10 });
        RuleForEach(x => x.Splits).ChildRules(split =>
        {
            split.RuleFor(x => x.Quantity).GreaterThan(0);
            split.RuleFor(x => x.Location).NotEmpty().MaximumLength(255);
            split.RuleFor(x => x.StockStatus).IsInEnum();
        });
    }
}
