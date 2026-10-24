namespace SaBai.API.Auth;

public sealed record WorkOsIdentity(string Id, string Email, string? FirstName, string? LastName);

public interface IWorkOsDirectory
{
    Task<WorkOsIdentity> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);

    Task<WorkOsIdentity> CreateUserAsync(
        string email,
        string password,
        string? firstName,
        string? lastName,
        string externalId,
        CancellationToken cancellationToken);

    Task UpdatePasswordAsync(string workOsUserId, string password, CancellationToken cancellationToken);
}
