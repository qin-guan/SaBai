using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Eyesight.ListByPatient.Get;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
    }
}
