using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.Storage;

namespace SaBai.API.Endpoints.Patients.Photo.Get;

public sealed class Endpoint(AppDbContext db, IAmazonS3 s3, RustFsSettings storage)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/patients/{Id}/photo");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Download a patient's profile photo");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db
            .Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);

        if (string.IsNullOrWhiteSpace(patient.PhotoStoredFileName))
        {
            throw new NotFoundException("Patient has no photo.");
        }

        try
        {
            using var obj = await s3.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = storage.Bucket,
                    Key = patient.PhotoStoredFileName,
                },
                ct
            );

            var contentType = string.IsNullOrWhiteSpace(obj.Headers.ContentType)
                ? "application/octet-stream"
                : obj.Headers.ContentType;

            await Send.StreamAsync(
                obj.ResponseStream,
                $"patient-{patient.Id}{Path.GetExtension(patient.PhotoStoredFileName)}",
                obj.ContentLength,
                contentType,
                cancellation: ct
            );
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Patient photo was not found.");
        }
    }
}
