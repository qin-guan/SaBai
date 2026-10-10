namespace SaBai.API.Endpoints.Auth.Users.Lock.Post;

/// <summary>Identifies the staff account to lock or unlock.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
