using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.ReferralPartners.Create.Post;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}
