using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Patients.Consents.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.ConsentType).IsInEnum();
    }
}
