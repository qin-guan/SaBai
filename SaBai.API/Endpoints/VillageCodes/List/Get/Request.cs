namespace SaBai.API.Endpoints.VillageCodes.List.Get;

/// <summary>Optional filter for the village code list.</summary>
public sealed record Request
{
    public bool IncludeHidden { get; init; }
}
