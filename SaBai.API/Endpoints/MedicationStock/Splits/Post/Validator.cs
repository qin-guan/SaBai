using FastEndpoints;
using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.MedicationStock.Splits.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Splits).NotNull().Must(x => x is { Count: >= 2 and <= 10 });
        RuleFor(x => x.Splits)
            .Must(x => x.Where(s => s.Id is not null).GroupBy(s => s.Id).All(g => g.Count() == 1))
            .WithMessage("Each split must have its own id.")
            .When(x => x.Splits is not null);
        RuleForEach(x => x.Splits)
            .ChildRules(split =>
            {
                split.RuleFor(x => x.Id).ClientId();
                split.RuleFor(x => x.Quantity).GreaterThan(0);
                split.RuleFor(x => x.Location).NotEmpty().MaximumLength(255);
                split.RuleFor(x => x.StockStatus).IsInEnum();
            });
    }
}
