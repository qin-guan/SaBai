using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Auth.Users.Lock.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Post("/auth/users/{Id}/lock");
        Roles(StaffRoles.Admin);
        Summary(s => s.Summary = "Lock a staff account");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await db.StaffUsers.FirstOrNotFoundAsync(x => x.Id == req.Id, "Staff user was not found.", ct);
        user.IsLocked = true;
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(UserResponse.From(user), ct);
    }
}
