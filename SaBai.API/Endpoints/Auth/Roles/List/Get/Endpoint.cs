using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;

namespace SaBai.API.Endpoints.Auth.Roles.List.Get;

public sealed class Endpoint(RoleManager<IdentityRole<Guid>> roles)
    : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/auth/roles");
        Roles(StaffRoles.Admin);
        Summary(s => s.Summary = "List assignable roles");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var names = await roles
            .Roles.Where(x => x.Name != null)
            .OrderBy(x => x.Name)
            .Select(x => x.Name!)
            .ToListAsync(ct);
        await Send.OkAsync(new Response(names), ct);
    }
}
