using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/patients/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db.Patients.FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);
        if (await db.Visits.AnyAsync(x => x.PatientId == patient.Id, ct))
        {
            throw new ConflictException("Patient has visits and cannot be deleted.");
        }

        db.Patients.Remove(patient);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
