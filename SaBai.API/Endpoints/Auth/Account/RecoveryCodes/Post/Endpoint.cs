using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Account.RecoveryCodes.Post;

public sealed class Endpoint(UserManager<StaffUser> users) : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Post("/auth/account/recovery-codes");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Replace your recovery codes");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await users.GetCurrentAsync(User);
        if (!await users.GetTwoFactorEnabledAsync(user))
        {
            throw new BadRequestException("Two-factor authentication is off.");
        }

        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await Send.OkAsync(new Response(codes?.ToList() ?? []), ct);
    }
}
