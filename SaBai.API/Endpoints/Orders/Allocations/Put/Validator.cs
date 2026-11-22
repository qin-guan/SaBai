using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Orders.Allocations.Put;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Allocations).NotNull().WithMessage("Choose at least one stock batch.");
        this.AllocationChoices(x => x.Allocations);
    }
}
