using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Patients.Faces.Get;

public sealed class Endpoint(AppDbContext db, FaceStore store) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/patients/{Id}/faces");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List the faces enrolled for a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);

        var faces = await store.ListAsync(PatientFaceIdentity.ForPatient(patient.Id), ct);
        await Send.OkAsync(new Response(patient.Id, faces.ToArray()), ct);
    }
}
