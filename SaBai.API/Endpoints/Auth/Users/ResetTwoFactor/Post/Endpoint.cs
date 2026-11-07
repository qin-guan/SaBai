using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;
using SaBai.API.Endpoints.Auth.Users.Lock.Post;

namespace SaBai.API.Endpoints.Auth.Users.ResetTwoFactor.Post;

public sealed class Endpoint(UserManager<StaffUser> users) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Post("/auth/users/{Id}/two-factor/reset");
        Roles(StaffRoles.Admin);
        Summary(s =>
        {
            s.Summary = "Turn off a staff user's two-factor authentication";
            s.Description =
                "For a lost phone: turns two-factor off and discards the authenticator secret, "
                + "so the user can sign in with just their password and set it up again.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.FindByIdOrNotFoundAsync(req.Id);
        (await users.SetTwoFactorEnabledAsync(user, false)).ThrowIfFailed();
        (await users.ResetAuthenticatorKeyAsync(user)).ThrowIfFailed();
        await Send.OkAsync(await UserResponse.FromAsync(user, users), ct);
    }
}
