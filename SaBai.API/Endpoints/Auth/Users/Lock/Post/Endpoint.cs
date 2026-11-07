using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Lock.Post;

public sealed class Endpoint(UserManager<StaffUser> users) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Post("/auth/users/{Id}/lock");
        Roles(StaffRoles.Admin);
        Summary(s =>
        {
            s.Summary = "Lock a staff account";
            s.Description =
                "Blocks sign-in until unlocked and ends the user's open sessions within a minute.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.FindByIdOrNotFoundAsync(req.Id);
        if (user.Id == User.Id())
        {
            throw new BadRequestException("You cannot lock your own account.");
        }

        (await users.SetLockoutEnabledAsync(user, true)).ThrowIfFailed();
        (await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)).ThrowIfFailed();
        // A new security stamp invalidates existing cookies at their next validation.
        (await users.UpdateSecurityStampAsync(user)).ThrowIfFailed();
        await Send.OkAsync(await UserResponse.FromAsync(user, users), ct);
    }
}
