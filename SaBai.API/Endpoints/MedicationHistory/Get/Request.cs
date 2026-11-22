namespace SaBai.API.Endpoints.MedicationHistory.Get;

/// <summary>Brand whose dispensed orders and quantity changes made by hand should be listed.</summary>
public sealed record Request
{
    public Guid? BrandId { get; init; }
}
