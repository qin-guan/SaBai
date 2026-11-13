using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Create.Post;

public sealed class Endpoint(UserManager<StaffUser> users) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Post("/auth/users");
        Roles(StaffRoles.Admin);
        Summary(s => s.Summary = "Create a staff user");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userName = req.UserName.Trim();
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email is not null && await users.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var user = new StaffUser
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            Email = email,
            EmailConfirmed = email is not null,
            Nickname = string.IsNullOrWhiteSpace(req.Nickname) ? userName : req.Nickname.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        (await users.CreateAsync(user, req.Password)).ThrowIfFailed();

        var roles = req.Roles is { Count: > 0 } ? req.Roles.Distinct() : [StaffRoles.Member];
        (await users.AddToRolesAsync(user, roles)).ThrowIfFailed();

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { user.Id },
            await UserResponse.FromAsync(user, users),
            cancellation: ct
        );
    }
}
