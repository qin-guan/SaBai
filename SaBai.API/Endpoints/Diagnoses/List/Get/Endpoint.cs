using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Diagnoses.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/diagnoses");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List diagnoses, optionally for one consult");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.Diagnoses.AsNoTracking().AsQueryable();
        if (req.ConsultId is > 0)
        {
            query = query.Where(x => x.ConsultId == req.ConsultId);
        }

        var diagnoses = await query
            .OrderBy(x => x.Id)
            .Select(x => new Response(x.Id, x.ConsultId, x.Details, x.Category))
            .ToListAsync(ct);

        await Send.OkAsync(diagnoses, ct);
    }
}
