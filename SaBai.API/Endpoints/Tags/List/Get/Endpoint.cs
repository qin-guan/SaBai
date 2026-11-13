using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Tags.List.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/tags");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "List all known freeform tags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tags = await db
            .Tags.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(ct);

        await Send.OkAsync(new Response(tags), ct);
    }
}
