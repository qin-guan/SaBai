using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Broadcasts.List.Get;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, Request.MaxLimit)
            .When(x => x.Limit is not null);
    }
}
