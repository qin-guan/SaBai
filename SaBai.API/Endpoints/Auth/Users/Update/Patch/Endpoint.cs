using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Update.Patch;

public sealed class Endpoint(UserManager<StaffUser> users) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Patch("/auth/users/{Id}");
        Roles(StaffRoles.Admin);
        Summary(s =>
        {
            s.Summary = "Update a staff user";
            s.Description =
                "Role changes reach the user's open sessions within a minute, without signing them out.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.FindByIdOrNotFoundAsync(req.Id);

        if (req.UserName is not null && req.UserName.Trim() != user.UserName)
        {
            (await users.SetUserNameAsync(user, req.UserName.Trim())).ThrowIfFailed();
        }

        if (req.Email is not null)
        {
            var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
            if (
                email is not null
                && await users.FindByEmailAsync(email) is { } other
                && other.Id != user.Id
            )
            {
                throw new ConflictException("A user with this email already exists.");
            }

            if (email != user.Email)
            {
                (await users.SetEmailAsync(user, email)).ThrowIfFailed();
                user.EmailConfirmed = email is not null;
            }
        }

        if (req.Nickname is not null)
        {
            user.Nickname = req.Nickname.Trim();
        }

        (await users.UpdateAsync(user)).ThrowIfFailed();

        if (req.Roles is not null)
        {
            var wanted = req.Roles.Distinct().ToList();
            var current = await users.GetRolesAsync(user);
            if (
                user.Id == User.Id()
                && current.Contains(StaffRoles.Admin)
                && !wanted.Contains(StaffRoles.Admin)
            )
            {
                throw new BadRequestException("You cannot remove your own admin role.");
            }

            var remove = current.Except(wanted).ToList();
            var add = wanted.Except(current).ToList();
            if (remove.Count > 0)
            {
                (await users.RemoveFromRolesAsync(user, remove)).ThrowIfFailed();
            }

            if (add.Count > 0)
            {
                (await users.AddToRolesAsync(user, add)).ThrowIfFailed();
            }
        }

        await Send.OkAsync(await UserResponse.FromAsync(user, users), ct);
    }
}
