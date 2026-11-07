using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Account.Password.Post;

public sealed class Endpoint(UserManager<StaffUser> users, SignInManager<StaffUser> signIn)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Post("/auth/account/password");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
        {
            s.Summary = "Change your password";
            s.Description = "Signs out your other sessions; this one stays signed in.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.GetCurrentAsync(User);
        var result = await users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (result.Errors.Any(x => x.Code == "PasswordMismatch"))
        {
            throw new BadRequestException("Current password is incorrect.");
        }

        result.ThrowIfFailed();
        await signIn.RefreshSignInAsync(user);
        await Send.NoContentAsync(ct);
    }
}
