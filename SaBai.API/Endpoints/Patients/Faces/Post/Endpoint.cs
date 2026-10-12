using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Endpoints.Faces;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Patients.Faces.Post;

public sealed class Endpoint(AppDbContext db, FaceAnalyzer analyzer, FaceStore store)
    : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/patients/{Id}/faces");
        AllowFileUploads();
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
        {
            s.Summary = "Enroll a face for a patient.";
            s.Description = "Uses the most prominent face in the image. Enroll several photos per patient for better accuracy.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db.Patients.FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);

        var image = await FaceImages.LoadAsync(this, req.Image, nameof(req.Image), ct);
        ThrowIfAnyErrors();

        var faces = await analyzer.AnalyzeAsync(image!, ct);
        var face = FaceImages.Largest(faces);
        if (face is null)
        {
            ThrowError(r => r.Image, "No usable face detected. Use a closer, sharp, well-lit photo.", StatusCodes.Status422UnprocessableEntity);
        }

        var stored = await store.AddAsync(PatientFaceIdentity.ForPatient(patient.Id), face.Embedding, ct);

        patient.LatestFaceId = stored.FaceId.ToString();
        await db.SaveChangesAsync(ct);

        await Send.ResponseAsync(
            new Response(stored.FaceId, patient.Id, FaceDto.From(face.Detection), faces.Count),
            StatusCodes.Status201Created,
            ct);
    }
}
