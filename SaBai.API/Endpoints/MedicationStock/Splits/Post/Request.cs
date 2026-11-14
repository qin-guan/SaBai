using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Splits.Post;

/// <summary>One child lot produced by splitting a parent stock row.</summary>
public sealed record SplitItem
{
    /// <summary>Id for this child lot, chosen by the client; generated when omitted. When every child id is sent and all already exist, the split was already made and is not made again.</summary>
    public Guid? Id { get; init; }

    public int Quantity { get; init; }

    public required string Location { get; init; }

    public StockStatus StockStatus { get; init; }

    public string? Remarks { get; init; }
}

/// <summary>Split a parent stock lot into two to ten distinct children.</summary>
public sealed record Request
{
    public Guid Id { get; init; }

    public List<SplitItem> Splits { get; init; } = [];
}
