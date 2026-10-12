using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Auth.Users.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Get("/auth/users/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a staff user");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await db.StaffUsers.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Staff user was not found.",
            ct
        );
        await Send.OkAsync(UserResponse.From(user), ct);
    }
}
