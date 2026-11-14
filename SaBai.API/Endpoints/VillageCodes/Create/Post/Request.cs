namespace SaBai.API.Endpoints.VillageCodes.Create.Post;

/// <summary>Payload for creating a village code.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string ColorHex { get; init; }

    public bool IsVisible { get; init; } = true;
}
