namespace SaBai.API.Endpoints.Auth.Users.Get;

/// <summary>Identifies a staff user.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
