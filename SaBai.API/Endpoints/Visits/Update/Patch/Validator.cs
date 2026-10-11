using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Visits.Update.Patch;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0).When(x => x.PatientId is not null);
        RuleFor(x => x.VillageCodeId).GreaterThan(0).When(x => x.VillageCodeId is not null);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(100).When(x => x.Status is not null);
    }
}
