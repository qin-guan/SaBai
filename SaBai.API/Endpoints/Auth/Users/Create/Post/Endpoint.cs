using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Create.Post;

public sealed class Endpoint(AppDbContext db, IWorkOsDirectory directory) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Post("/auth/users");
        Roles(StaffRoles.Admin);
        Summary(s => s.Summary = "Create a staff user in WorkOS");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var email = req.Email.Trim();
        var username = req.Username.Trim();
        if (await db.StaffUsers.AnyAsync(x => x.Email == email || x.Username == username, ct))
        {
            throw new ConflictException("A user with this email or username already exists.");
        }

        var id = Guid.CreateVersion7();
        var identity = await directory.CreateUserAsync(email, req.Password, req.Nickname, null, id.ToString(), ct);
        var user = new StaffUser
        {
            Id = id,
            WorkOsUserId = identity.Id,
            Username = username,
            Email = email,
            Nickname = string.IsNullOrWhiteSpace(req.Nickname) ? username : req.Nickname.Trim(),
            Role = req.Role,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.StaffUsers.Add(user);
        await db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<Get.Endpoint>(new { user.Id }, UserResponse.From(user), cancellation: ct);
    }
}
