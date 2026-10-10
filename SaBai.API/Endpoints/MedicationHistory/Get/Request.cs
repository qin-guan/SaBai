namespace SaBai.API.Endpoints.MedicationHistory.Get;

/// <summary>Brand whose approved dispenses and quantity adjustments should be listed.</summary>
public sealed record Request
{
    public int? BrandId { get; init; }
}
