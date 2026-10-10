namespace SaBai.API.Endpoints.VillageCodes.Update.Patch;

/// <summary>Partial update for a village code. Omitted fields are left unchanged.</summary>
public sealed record Request
{
    public int Id { get; init; }

    public string? Code { get; init; }

    public string? Name { get; init; }

    public string? ColorHex { get; init; }

    public bool? IsVisible { get; init; }
}
