using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.WhoAmI.Get;

public sealed class Endpoint(UserManager<StaffUser> users, AppDbContext db)
    : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/auth/whoami");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "Return the signed-in staff user");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await users.GetCurrentAsync(User);
        await Send.OkAsync(new Response(await UserResponse.FromAsync(user, users, db, ct)), ct);
    }
}
