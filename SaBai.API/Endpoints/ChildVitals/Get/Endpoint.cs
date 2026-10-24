using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.ChildVitals.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/child-vitals/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get child vitals by id");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db
            .ChildVitals.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Child vitals were not found.", ct);
        await Send.OkAsync(Response.From(entity), ct);
    }
}
