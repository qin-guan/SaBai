using System.Diagnostics;
using Microsoft.Extensions.Options;
using SaBai.API.Domain;
using WorkOS;

namespace SaBai.API.Auth;

public sealed class WorkOsDirectory(
    IOptions<WorkOsSettings> options,
    ILogger<WorkOsDirectory> logger
) : IWorkOsDirectory
{
    private readonly WorkOSClient _client = new(
        new WorkOSOptions { ApiKey = options.Value.ApiKey, ClientId = options.Value.ClientId }
    );

    public Task<WorkOsIdentity> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken
    ) =>
        CallAsync(
            "authenticate",
            email,
            async () =>
            {
                var response = await _client.UserManagement.AuthenticateWithPasswordAsync(
                    new AuthenticateWithPasswordOptions { Email = email, Password = password },
                    cancellationToken: cancellationToken
                );
                return Map(response.User);
            },
            rejectAsInvalidCredentials: true
        );

    public Task<WorkOsIdentity> CreateUserAsync(
        string email,
        string password,
        string? firstName,
        string? lastName,
        string externalId,
        CancellationToken cancellationToken
    ) =>
        CallAsync(
            "create-user",
            email,
            async () =>
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
                    cancellationToken: cancellationToken
                );
                return new WorkOsIdentity(
                    created.Id,
                    created.Email,
                    created.FirstName,
                    created.LastName
                );
            },
            rejectAsInvalidCredentials: false
        );

    public Task UpdatePasswordAsync(
        string workOsUserId,
        string password,
        CancellationToken cancellationToken
    ) =>
        CallAsync(
            "update-password",
            workOsUserId,
            async () =>
            {
                await _client.UserManagement.UpdateAsync(
                    workOsUserId,
                    new UserManagementUpdateOptions
                    {
                        Password = new UserManagementPasswordPlaintext { Password = password },
                    },
                    cancellationToken: cancellationToken
                );
                return true;
            },
            rejectAsInvalidCredentials: false
        );

    private async Task<T> CallAsync<T>(
        string operation,
        string subject,
        Func<Task<T>> call,
        bool rejectAsInvalidCredentials
    )
    {
        using var activity = WorkOsTelemetry.Start(operation, subject);
        logger.LogInformation("Calling WorkOS {Operation} for {Subject}", operation, subject);
        try
        {
            var result = await call();
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception exception) when (exception is not AuthRejectedException)
        {
            WorkOsTelemetry.Fail(activity, exception);
            logger.LogWarning(
                exception,
                "WorkOS {Operation} failed for {Subject}. Status {StatusCode}. Code {ErrorCode}. Body {Body}",
                operation,
                subject,
                activity?.GetTagItem("workos.status_code"),
                activity?.GetTagItem("workos.error_code"),
                activity?.GetTagItem("workos.response_body")
            );
            if (rejectAsInvalidCredentials && IsRejected(exception))
            {
                throw new AuthRejectedException();
            }

            throw;
        }
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
