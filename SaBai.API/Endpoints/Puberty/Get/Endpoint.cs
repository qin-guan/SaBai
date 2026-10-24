using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Puberty.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/puberty/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a puberty assessment by id");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db
            .PubertyAssessments.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Puberty assessment was not found.", ct);
        await Send.OkAsync(Response.From(entity), ct);
    }
}
