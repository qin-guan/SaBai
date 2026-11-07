using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.ResetPassword.Post;

public sealed class Endpoint(UserManager<StaffUser> users) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Post("/auth/users/{Id}/password");
        Roles(StaffRoles.Admin);
        Summary(s =>
        {
            s.Summary = "Set a staff user's password";
            s.Description =
                "Replaces the password (or sets one for accounts without), and ends the "
                + "user's open sessions within a minute.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.FindByIdOrNotFoundAsync(req.Id);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        (await users.ResetPasswordAsync(user, token, req.Password)).ThrowIfFailed();
        await Send.OkAsync(await UserResponse.FromAsync(user, users), ct);
    }
}
