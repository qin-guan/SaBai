using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.MedicationStock.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Location).MaximumLength(255);
        RuleFor(x => x.StockStatus).IsInEnum().When(x => x.StockStatus is not null);
    }
}
