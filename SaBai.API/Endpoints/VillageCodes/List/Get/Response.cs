namespace SaBai.API.Endpoints.VillageCodes.List.Get;

/// <summary>Village codes, newest first. Hidden codes are omitted unless requested.</summary>
public sealed record Response(IReadOnlyList<Item> VillageCodes);

/// <summary>A village code.</summary>
public sealed record Item(
    int Id,
    string Code,
    string Name,
    string ColorHex,
    bool IsVisible,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
