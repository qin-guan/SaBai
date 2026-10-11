using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Storage;

namespace SaBai.API.Endpoints.Patients.Photo.Put;

public sealed class Endpoint(AppDbContext db, IAmazonS3 s3, RustFsSettings storage, ILogger<Endpoint> logger)
    : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Put("/patients/{Id}/photo");
        AllowFileUploads();
        MaxRequestBodySize(PatientPhoto.MaxBytes + 1024);
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Set or replace a patient's profile photo");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db.Patients.FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);

        var bytes = await PatientPhoto.ReadValidatedAsync(this, req.Image, nameof(req.Image), ct);
        ThrowIfAnyErrors();

        var extension = PatientPhoto.Extension(req.Image.FileName);
        var key = PatientPhoto.BuildKey(patient.Id, req.Image.FileName);

        await using (var stream = new MemoryStream(bytes!))
        {
            await s3.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = storage.Bucket,
                    Key = key,
                    InputStream = stream,
                    ContentType = PatientPhoto.ContentType(req.Image.ContentType, extension),
                    AutoCloseStream = false,
                },
                ct);
        }

        var previousKey = patient.PhotoStoredFileName;
        patient.PhotoStoredFileName = key;
        await db.SaveChangesAsync(ct);

        if (previousKey != key)
        {
            await PatientPhoto.DeleteObjectAsync(s3, storage, previousKey, logger, ct);
        }

        await Send.OkAsync(new Response(patient.Id, true), ct);
    }
}
