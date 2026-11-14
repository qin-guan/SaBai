namespace SaBai.API.Domain;

/// <summary>
/// One browser or installed app that a staff user allowed to receive notifications
/// (a Web Push subscription). A user has one row per device.
/// </summary>
public sealed class StaffPushSubscription
{
    public int Id { get; set; }

    public Guid StaffUserId { get; set; }

    public StaffUser? StaffUser { get; set; }

    /// <summary>URL at the browser vendor's push service that messages are posted to.</summary>
    public required string Endpoint { get; set; }

    /// <summary>The device's public key (base64url), used to encrypt messages to it.</summary>
    public required string P256dhKey { get; set; }

    /// <summary>The device's authentication secret (base64url).</summary>
    public required string AuthSecret { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
