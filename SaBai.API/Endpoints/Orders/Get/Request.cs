namespace SaBai.API.Endpoints.Orders.Get;

/// <summary>Identifies the medication order to read.</summary>
public sealed record Request
{
    public int Id { get; init; }
}
