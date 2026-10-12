using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Update.Patch;

public sealed class Endpoint(AppDbContext db, IWorkOsDirectory directory)
    : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Patch("/auth/users/{Id}");
        Roles(StaffRoles.Admin);
        Summary(s => s.Summary = "Update a staff user");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await db.StaffUsers.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Staff user was not found.",
            ct
        );

        if (req.Username is not null)
        {
            var username = req.Username.Trim();
            if (await db.StaffUsers.AnyAsync(x => x.Id != user.Id && x.Username == username, ct))
            {
                throw new ConflictException("A user with this username already exists.");
            }

            user.Username = username;
        }

        if (req.Email is not null)
        {
            var email = req.Email.Trim();
            if (await db.StaffUsers.AnyAsync(x => x.Id != user.Id && x.Email == email, ct))
            {
                throw new ConflictException("A user with this email already exists.");
            }

            user.Email = email;
        }

        if (req.Nickname is not null)
        {
            user.Nickname = req.Nickname.Trim();
        }

        if (req.Role is { } role)
        {
            user.Role = role;
        }

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            await directory.UpdatePasswordAsync(user.WorkOsUserId, req.Password, ct);
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(UserResponse.From(user), ct);
    }
}
