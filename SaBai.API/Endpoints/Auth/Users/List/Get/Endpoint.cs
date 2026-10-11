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
        var query = db.StaffUsers.AsNoTracking().AsQueryable();
        if (req.Role is { } role)
        {
            query = query.Where(x => x.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            query = query.Where(x => x.Email == req.Email);
        }

        if (req.IsLocked is { } locked)
        {
            query = query.Where(x => x.IsLocked == locked);
        }

        var users = await query
            .OrderBy(x => x.Nickname)
            .Select(x => UserResponse.From(x))
            .ToListAsync(ct);

        await Send.OkAsync(new Response(users), ct);
    }
}
