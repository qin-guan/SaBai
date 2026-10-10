using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Visits.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.VillageCodeId).GreaterThan(0);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(100).When(x => x.Status is not null);
    }
}
