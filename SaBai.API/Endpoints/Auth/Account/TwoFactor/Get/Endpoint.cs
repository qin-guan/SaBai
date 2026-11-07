using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Account.TwoFactor.Get;

public sealed class Endpoint(UserManager<StaffUser> users, SignInManager<StaffUser> signIn)
    : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/auth/account/two-factor");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get your two-factor status");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await users.GetCurrentAsync(User);
        await Send.OkAsync(
            new Response(
                await users.GetTwoFactorEnabledAsync(user),
                await users.CountRecoveryCodesAsync(user),
                await signIn.IsTwoFactorClientRememberedAsync(user)
            ),
            ct
        );
    }
}
