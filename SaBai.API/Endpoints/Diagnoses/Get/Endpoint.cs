using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Diagnoses.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/diagnoses/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a diagnosis");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var diagnosis = await db.Diagnoses.FirstOrNotFoundAsync(x => x.Id == req.Id, "Diagnosis was not found.", ct);
        await Send.OkAsync(Response.From(diagnosis), ct);
    }
}
