namespace SaBai.API.Endpoints.Auth.Users.Lock.Post;

/// <summary>Identifies a staff account for an admin action.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
