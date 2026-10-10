using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Splits.Post;

/// <summary>One child lot produced by splitting a parent stock row.</summary>
public sealed record SplitItem
{
    public int Quantity { get; init; }

    public required string Location { get; init; }

    public StockStatus StockStatus { get; init; }

    public string? Remarks { get; init; }
}

/// <summary>Split a parent stock lot into two to ten distinct children.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public List<SplitItem> Splits { get; init; } = [];
}
