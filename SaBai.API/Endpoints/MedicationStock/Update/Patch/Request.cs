using SaBai.API.Domain;

namespace SaBai.API.Endpoints.MedicationStock.Update.Patch;

/// <summary>Location, status, and remarks update. Blank or omitted remarks are cleared.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? Location { get; init; }

    public StockStatus? StockStatus { get; init; }

    public string? Remarks { get; init; }
}
