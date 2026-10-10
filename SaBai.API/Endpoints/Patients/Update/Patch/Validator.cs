using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Patients.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).When(x => x.Name is not null);
        RuleFor(x => x.IdentificationNumber).MaximumLength(255);
        RuleFor(x => x.ContactNo).MaximumLength(255);
        RuleFor(x => x.Gender).IsInEnum().When(x => x.Gender is not null);
        RuleFor(x => x.DrugAllergy).NotEmpty().When(x => x.DrugAllergy is not null);
        RuleFor(x => x.VillageCodeId).GreaterThan(0).When(x => x.VillageCodeId is not null);
    }
}
