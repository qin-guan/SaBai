using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.AuditLogs.List.Get;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Take).InclusiveBetween(1, Endpoint.MaxTake).When(x => x.Take is not null);
        RuleFor(x => x.To)
            .GreaterThan(x => x.From)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("'to' must be after 'from'.");
    }
}
