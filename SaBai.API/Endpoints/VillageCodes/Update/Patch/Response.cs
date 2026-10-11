namespace SaBai.API.Endpoints.VillageCodes.Update.Patch;

/// <summary>A village code after a partial update.</summary>
public sealed record Response(
    int Id,
    string Code,
    string Name,
    string ColorHex,
    bool IsVisible,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
