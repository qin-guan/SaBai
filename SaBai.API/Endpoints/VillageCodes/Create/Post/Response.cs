namespace SaBai.API.Endpoints.VillageCodes.Create.Post;

/// <summary>A newly created village code.</summary>
public sealed record Response(
    int Id,
    string Code,
    string Name,
    string ColorHex,
    bool IsVisible,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
