using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.MedicationStock.Adjustments.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
