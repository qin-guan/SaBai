using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Endpoints.Auth.Users.Lock.Post;

namespace SaBai.API.Endpoints.Auth.Users.Unlock.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Post("/auth/users/{Id}/unlock");
        Roles(StaffRoles.Admin);
        Summary(s => s.Summary = "Unlock a staff account");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await db.StaffUsers.FirstOrNotFoundAsync(x => x.Id == req.Id, "Staff user was not found.", ct);
        user.IsLocked = false;
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(UserResponse.From(user), ct);
    }
}
