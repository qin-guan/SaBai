namespace SaBai.API.Endpoints.VillageCodes.Create.Post;

/// <summary>Payload for creating a village code.</summary>
public sealed record Request
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string ColorHex { get; init; }

    public bool IsVisible { get; init; } = true;
}
