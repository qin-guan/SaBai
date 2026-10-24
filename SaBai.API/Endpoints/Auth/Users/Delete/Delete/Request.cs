namespace SaBai.API.Endpoints.Auth.Users.Delete.Delete;

/// <summary>Identifies a staff user that must not be deleted.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
