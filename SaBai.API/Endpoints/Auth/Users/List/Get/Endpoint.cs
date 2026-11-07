using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Auth.Users.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/auth/users");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List staff users");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Role))
        {
            var role = req.Role.Trim().ToUpperInvariant();
            query = query.Where(user =>
                db.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id
                    && db.Roles.Any(x => x.Id == userRole.RoleId && x.NormalizedName == role)
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            var email = req.Email.Trim().ToUpperInvariant();
            query = query.Where(x => x.NormalizedEmail == email);
        }

        if (req.IsLocked is { } locked)
        {
            var now = DateTimeOffset.UtcNow;
            query = locked
                ? query.Where(x => x.LockoutEnabled && x.LockoutEnd != null && x.LockoutEnd > now)
                : query.Where(x => !x.LockoutEnabled || x.LockoutEnd == null || x.LockoutEnd <= now);
        }

        var users = await UserResponse.ListAsync(
            db,
            query.OrderBy(x => x.Nickname).ThenBy(x => x.UserName),
            ct
        );
        await Send.OkAsync(new Response(users), ct);
    }
}
