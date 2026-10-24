using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Consults.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/consults/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a consult");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var consult = await db
            .Consults.AsNoTracking()
            .Include(x => x.Doctor)
            .Include(x => x.Diagnoses)
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Consult was not found.", ct);

        await Send.OkAsync(Response.From(consult), ct);
    }
}
