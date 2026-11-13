using FastEndpoints;
using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Patients.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.IdentificationNumber).MaximumLength(255);
        RuleFor(x => x.ContactNo).MaximumLength(255);
        RuleFor(x => x.Gender).NotNull().IsInEnum();
        RuleFor(x => x.DateOfBirth).NotNull();
        RuleFor(x => x.DrugAllergy).NotEmpty();
        RuleFor(x => x.VillageCodeId).GreaterThan(0);
        RuleForEach(x => x.Tags)
            .Must(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length <= TagSync.MaxTagLength)
            .WithMessage($"Tags must be {TagSync.MaxTagLength} characters or fewer.");
    }
}
