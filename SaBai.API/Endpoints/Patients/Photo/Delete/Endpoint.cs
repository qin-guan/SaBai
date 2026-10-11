using Amazon.S3;
using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Storage;

namespace SaBai.API.Endpoints.Patients.Photo.Delete;

public sealed class Endpoint(AppDbContext db, IAmazonS3 s3, RustFsSettings storage, ILogger<Endpoint> logger)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/patients/{Id}/photo");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Remove a patient's profile photo");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db.Patients.FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);

        var key = patient.PhotoStoredFileName;
        patient.PhotoStoredFileName = null;
        await db.SaveChangesAsync(ct);

        await PatientPhoto.DeleteObjectAsync(s3, storage, key, logger, ct);
        await Send.NoContentAsync(ct);
    }
}
