namespace SaBai.API.Endpoints.Push.Unsubscribe.Post;

/// <summary>The push subscription endpoint of the device to stop notifying.</summary>
public sealed record Request
{
    public required string Endpoint { get; init; }
}
