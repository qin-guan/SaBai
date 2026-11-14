using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Patients.Disclosures.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.Recipient).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(255);
    }
}
