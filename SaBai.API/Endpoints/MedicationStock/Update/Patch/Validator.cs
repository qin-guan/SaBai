using FastEndpoints;
using FluentValidation;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Location).MaximumLength(255);
        RuleFor(x => x.StockStatus)
            .IsInEnum()
            .Must(status => StockStatusRules.CanBeChosen(status!.Value))
            .WithMessage(StockStatusRules.CannotChooseMessage)
            .When(x => x.StockStatus is not null);
    }
}
