using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.WhoAmI.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/auth/whoami");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Return the signed-in staff user");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await db.StaffUsers.FirstOrNotFoundAsync(
            x => x.Id == User.Id(),
            "Staff user was not found.",
            ct
        );
        await Send.OkAsync(new Response(UserResponse.From(user)), ct);
    }
}
