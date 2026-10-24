using System.Collections.Concurrent;
using System.Diagnostics;
using SaBai.API.Domain;

namespace SaBai.API.Auth;

public sealed class FakeWorkOsDirectory(ILogger<FakeWorkOsDirectory> logger) : IWorkOsDirectory
{
    private readonly ConcurrentDictionary<string, FakeUser> _byEmail = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, FakeUser> _byId = new(StringComparer.Ordinal);

    public void Remember(string id, string email, string password)
    {
        var user = new FakeUser(id, email, password);
        _byEmail[email] = user;
        _byId[id] = user;
    }

    public Task<WorkOsIdentity> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken
    )
    {
        using var activity = WorkOsTelemetry.Start("authenticate", email);
        activity?.SetTag("workos.fake", true);
        if (!_byEmail.TryGetValue(email, out var user) || user.Password != password)
        {
            var rejected = new AuthRejectedException();
            WorkOsTelemetry.Fail(activity, rejected);
            logger.LogWarning(
                "Fake WorkOS rejected {Email}. The API is not calling WorkOS.",
                email
            );
            throw rejected;
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        logger.LogInformation(
            "Fake WorkOS accepted {Email}. The API is not calling WorkOS.",
            email
        );
        return Task.FromResult(new WorkOsIdentity(user.Id, user.Email, null, null));
    }

    public Task<WorkOsIdentity> CreateUserAsync(
        string email,
        string password,
        string? firstName,
        string? lastName,
        string externalId,
        CancellationToken cancellationToken
    )
    {
        if (_byEmail.ContainsKey(email))
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var id = $"user_{externalId}";
        Remember(id, email, password);
        return Task.FromResult(new WorkOsIdentity(id, email, firstName, lastName));
    }

    public Task UpdatePasswordAsync(
        string workOsUserId,
        string password,
        CancellationToken cancellationToken
    )
    {
        if (!_byId.TryGetValue(workOsUserId, out var user))
        {
            throw new NotFoundException("WorkOS user was not found.");
        }

        Remember(user.Id, user.Email, password);
        return Task.CompletedTask;
    }

    private sealed record FakeUser(string Id, string Email, string Password);
}
