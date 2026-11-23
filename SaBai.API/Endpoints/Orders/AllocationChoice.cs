using FluentValidation;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Orders;

/// <summary>Units of one stock batch the pharmacist chose to dispense an order from.</summary>
public sealed record AllocationChoice
{
    public Guid StockId { get; init; }

    public int Quantity { get; init; }
}

public static class AllocationChoiceRules
{
    /// <summary>
    /// The shape of a pharmacist's choice of batches: at least one, each batch once, each with a
    /// quantity of at least 1. Whether they add up to the order and can be reserved is checked
    /// against the order and the stock (<see cref="OrderLifecycle.ReallocateAsync"/>).
    /// </summary>
    public static void AllocationChoices<T>(
        this AbstractValidator<T> validator,
        Func<T, IReadOnlyList<AllocationChoice>?> choices
    )
    {
        validator
            .RuleFor(x => choices(x))
            .Must(x => x is not { Count: 0 })
            .WithName("Allocations")
            .WithMessage("Choose at least one stock batch.")
            .Must(x => x is not { Count: > 50 })
            .WithName("Allocations")
            .WithMessage("Choose at most 50 stock batches.")
            .Must(x => x is null || x.All(a => a is not null && a.StockId != Guid.Empty))
            .WithName("Allocations")
            .WithMessage("Each chosen batch needs a stockId.")
            .Must(x => x is null || x.All(a => a is not null && a.Quantity > 0))
            .WithName("Allocations")
            .WithMessage("Each chosen batch needs a quantity of at least 1.")
            .Must(x =>
                x is null
                || x.Where(a => a is not null).GroupBy(a => a.StockId).All(g => g.Count() == 1)
            )
            .WithName("Allocations")
            .WithMessage("Each stock batch can only be chosen once.");
    }

    public static IReadOnlyList<AllocationPick> ToPicks(this IReadOnlyList<AllocationChoice> choices) =>
        [.. choices.Select(x => new AllocationPick(x.StockId, x.Quantity))];
}
