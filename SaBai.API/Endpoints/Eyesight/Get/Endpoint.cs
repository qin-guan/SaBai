using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Eyesight.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/eyesight/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get eyesight by id");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db
            .Eyesights.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Eyesight was not found.", ct);
        await Send.OkAsync(Response.From(entity), ct);
    }
}
