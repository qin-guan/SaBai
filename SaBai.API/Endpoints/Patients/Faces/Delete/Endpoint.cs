using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Patients.Faces.Delete;

public sealed class Endpoint(AppDbContext db, FaceStore store) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/patients/{Id}/faces/{FaceId}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete an enrolled face for a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db.Patients.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Patient was not found.",
            ct
        );

        var faces = await store.ListAsync(PatientFaceIdentity.ForPatient(patient.Id), ct);
        if (faces.All(f => f.FaceId != req.FaceId))
        {
            throw new NotFoundException("Face was not found for this patient.");
        }

        await store.DeleteFaceAsync(req.FaceId, ct);

        if (patient.LatestFaceId == req.FaceId.ToString())
        {
            patient.LatestFaceId = faces
                .Where(f => f.FaceId != req.FaceId)
                .OrderBy(f => f.CreatedAt)
                .LastOrDefault()
                ?.FaceId.ToString();
            await db.SaveChangesAsync(ct);
        }

        await Send.NoContentAsync(ct);
    }
}
