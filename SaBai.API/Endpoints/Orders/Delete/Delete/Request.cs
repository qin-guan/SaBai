namespace SaBai.API.Endpoints.Orders.Delete.Delete;

/// <summary>Identifies the medication order to delete. Stock is not reversed.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
