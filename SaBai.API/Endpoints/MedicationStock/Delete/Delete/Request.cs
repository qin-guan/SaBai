namespace SaBai.API.Endpoints.MedicationStock.Delete.Delete;

/// <summary>Identifies the stock lot to delete.</summary>
public sealed record Request
{
    public int Id { get; init; }
}
