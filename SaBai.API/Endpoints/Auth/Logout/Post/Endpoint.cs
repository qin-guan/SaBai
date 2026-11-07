using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Logout.Post;

public sealed class Endpoint(SignInManager<StaffUser> signIn, AuditTrail audit)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/auth/logout");
        AllowAnonymous();
        Summary(s => s.Summary = "Sign out and clear the session cookie");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            await audit.RecordAsync(AuditAction.SignedOut, ct: ct);
        }

        await signIn.SignOutAsync();
        await Send.NoContentAsync(ct);
    }
}
