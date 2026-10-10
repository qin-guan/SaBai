using Microsoft.Extensions.Options;
using SaBai.API.Domain;
using WorkOS;

namespace SaBai.API.Auth;

public sealed class WorkOsDirectory : IWorkOsDirectory
{
    private readonly WorkOSClient _client;

    public WorkOsDirectory(IOptions<WorkOsSettings> options)
    {
        var settings = options.Value;
        _client = new WorkOSClient(new WorkOSOptions
        {
            ApiKey = settings.ApiKey,
            ClientId = settings.ClientId,
        });
    }

    public async Task<WorkOsIdentity> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.UserManagement.AuthenticateWithPasswordAsync(
                new AuthenticateWithPasswordOptions
                {
                    Email = email,
                    Password = password,
                },
                cancellationToken: cancellationToken);

            return Map(response.User);
        }
        catch (AuthRejectedException)
        {
            throw;
        }
        catch (Exception exception) when (IsRejected(exception))
        {
            throw new AuthRejectedException();
        }
    }

    public async Task<WorkOsIdentity> CreateUserAsync(
        string email,
        string password,
        string? firstName,
        string? lastName,
        string externalId,
        CancellationToken cancellationToken)
    {
        var created = await _client.UserManagement.CreateAsync(
            new UserManagementCreateOptions
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailVerified = true,
                ExternalId = externalId,
                Password = new UserManagementPasswordPlaintext { Password = password },
            },
            cancellationToken: cancellationToken);

        return new WorkOsIdentity(created.Id, created.Email, created.FirstName, created.LastName);
    }

    public async Task UpdatePasswordAsync(
        string workOsUserId,
        string password,
        CancellationToken cancellationToken)
    {
        await _client.UserManagement.UpdateAsync(
            workOsUserId,
            new UserManagementUpdateOptions
            {
                Password = new UserManagementPasswordPlaintext { Password = password },
            },
            cancellationToken: cancellationToken);
    }

    private static WorkOsIdentity Map(User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName);

    private static bool IsRejected(Exception exception)
    {
        var name = exception.GetType().Name;
        return name.Contains("WorkOS", StringComparison.Ordinal)
            || name.Contains("Api", StringComparison.Ordinal)
            || exception.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase);
    }
}
