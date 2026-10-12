using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.VillageCodes.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/village-codes");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List village codes");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.VillageCodes.AsNoTracking().AsQueryable();
        if (!req.IncludeHidden)
        {
            query = query.Where(x => x.IsVisible);
        }

        var codes = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new Item(
                x.Id,
                x.Code,
                x.Name,
                x.ColorHex,
                x.IsVisible,
                x.CreatedAt,
                x.UpdatedAt
            ))
            .ToListAsync(ct);

        await Send.OkAsync(new Response(codes), ct);
    }
}
