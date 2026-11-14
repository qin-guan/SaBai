using FastEndpoints;
using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Patients.Disclosures.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).ClientId();
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Recipient).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(255);
    }
}
