using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Users.Get;

public sealed class Endpoint(UserManager<StaffUser> users) : Endpoint<Request, UserResponse>
{
    public override void Configure()
    {
        Get("/auth/users/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "Get a staff user");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.FindByIdOrNotFoundAsync(req.Id);
        await Send.OkAsync(await UserResponse.FromAsync(user, users), ct);
    }
}
