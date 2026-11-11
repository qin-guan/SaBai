using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Broadcasts.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/broadcasts");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List broadcasts, newest first");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.Broadcasts.AsNoTracking().AsQueryable();
        if (req.Since is int since)
        {
            query = query.Where(x => x.Id > since);
        }

        var broadcasts = await query
            .OrderByDescending(x => x.Id)
            .Take(req.Limit ?? Request.DefaultLimit)
            .Select(x => new Response(
                x.Id,
                x.Title,
                x.Body,
                x.Severity,
                x.CreatedBy,
                x.CreatedAt,
                x.PublishedAt,
                x.Attempts
            ))
            .ToListAsync(ct);

        await Send.OkAsync(broadcasts, ct);
    }
}
