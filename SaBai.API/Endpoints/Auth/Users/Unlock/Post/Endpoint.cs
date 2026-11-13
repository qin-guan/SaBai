using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.Endpoints.Auth.Users.Lock.Post;

namespace SaBai.API.Endpoints.Auth.Users.Unlock.Post;

public sealed class Endpoint(UserManager<StaffUser> users, AppDbContext db)
    : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Post("/auth/users/{Id}/unlock");
        Roles(StaffRoles.Admin);
        Summary(s =>
        {
            s.Summary = "Unlock a staff account";
            s.Description = "Also clears a temporary lock from too many failed sign-ins.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.FindByIdOrNotFoundAsync(req.Id);
        (await users.SetLockoutEndDateAsync(user, null)).ThrowIfFailed();
        (await users.ResetAccessFailedCountAsync(user)).ThrowIfFailed();
        await Send.OkAsync(await UserResponse.FromAsync(user, users, db, ct), ct);
    }
}
