using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using Microsoft.EntityFrameworkCore;

namespace SaBai.API.Endpoints.VillageCodes.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/village-codes/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a village code");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var village = await db.VillageCodes.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Village code was not found.", ct);

        await Send.OkAsync(
            new Response(village.Id, village.Code, village.Name, village.ColorHex, village.IsVisible, village.CreatedAt, village.UpdatedAt),
            ct);
    }
}
