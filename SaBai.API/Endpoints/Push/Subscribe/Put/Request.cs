namespace SaBai.API.Endpoints.Push.Subscribe.Put;

/// <summary>
/// A browser's push subscription, in the shape of <c>PushSubscription.toJSON()</c>.
/// </summary>
public sealed record Request
{
    public required string Endpoint { get; init; }

    public required SubscriptionKeys Keys { get; init; }
}

public sealed record SubscriptionKeys
{
    public required string P256dh { get; init; }

    public required string Auth { get; init; }
}
