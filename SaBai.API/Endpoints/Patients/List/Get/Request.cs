namespace SaBai.API.Endpoints.Patients.List.Get;

/// <summary>Optional name and village-code filters for the patient list.</summary>
public sealed record Request
{
    public string? Name { get; init; }

    public string? VillageCode { get; init; }
}
