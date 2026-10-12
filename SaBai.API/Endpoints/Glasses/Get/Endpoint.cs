using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Glasses.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/glasses/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get prescribed glasses by id");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var eyesight = await db
            .Eyesights.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Glasses were not found.", ct);
        await Send.OkAsync(Response.From(eyesight), ct);
    }
}
