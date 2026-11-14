using FastEndpoints;
using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Visits.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).ClientId();
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.VillageCodeId).NotEmpty();
        RuleFor(x => x.Status).NotEmpty().MaximumLength(100).When(x => x.Status is not null);
    }
}
