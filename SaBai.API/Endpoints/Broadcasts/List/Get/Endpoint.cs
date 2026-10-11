using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Broadcasts.List.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/broadcasts");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List broadcasts");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var broadcasts = await db.Broadcasts
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new Response(x.Id, x.Title, x.Body, x.Severity, x.CreatedBy, x.CreatedAt, x.PublishedAt, x.Attempts))
            .ToListAsync(ct);

        await Send.OkAsync(broadcasts, ct);
    }
}
