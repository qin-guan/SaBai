using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Diagnoses.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/diagnoses/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a diagnosis");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var diagnosis = await db.Diagnoses.FirstOrNotFoundAsync(x => x.Id == req.Id, "Diagnosis was not found.", ct);
        db.Diagnoses.Remove(diagnosis);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
