using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.WhoAmI.Get;

public sealed class Endpoint(UserManager<StaffUser> users) : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/auth/whoami");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Return the signed-in staff user");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await users.GetCurrentAsync(User);
        await Send.OkAsync(new Response(await UserResponse.FromAsync(user, users)), ct);
    }
}
