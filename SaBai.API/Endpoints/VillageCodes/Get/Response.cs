namespace SaBai.API.Endpoints.VillageCodes.Get;

/// <summary>A village code.</summary>
public sealed record Response(
    int Id,
    string Code,
    string Name,
    string ColorHex,
    bool IsVisible,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
