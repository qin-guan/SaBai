using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Account.TwoFactorDisable.Post;

public sealed class Endpoint(UserManager<StaffUser> users, SignInManager<StaffUser> signIn)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Post("/auth/account/two-factor/disable");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Turn off two-factor authentication");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.GetCurrentAsync(User);
        if (!await users.CheckPasswordAsync(user, req.Password))
        {
            throw new BadRequestException("Password is incorrect.");
        }

        (await users.SetTwoFactorEnabledAsync(user, false)).ThrowIfFailed();
        await signIn.ForgetTwoFactorClientAsync();
        await signIn.RefreshSignInAsync(user);
        await Send.NoContentAsync(ct);
    }
}
