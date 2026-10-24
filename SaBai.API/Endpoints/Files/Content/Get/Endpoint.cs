using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.Storage;

namespace SaBai.API.Endpoints.Files.Content.Get;

public sealed class Endpoint(AppDbContext db, IAmazonS3 s3, RustFsSettings storage) : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/files/{Id}/content");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Download a clinical file from RustFS");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var file = await db.ClinicalFiles
            .FirstOrNotFoundAsync(x => x.Id == req.Id && !x.IsDeleted, "File was not found.", ct);

        try
        {
            var obj = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = storage.Bucket,
                Key = file.StoredFileName,
            }, ct);

            await Send.StreamAsync(
                obj.ResponseStream,
                file.FileName,
                file.SizeBytes,
                string.IsNullOrWhiteSpace(obj.Headers.ContentType)
                    ? "application/octet-stream"
                    : obj.Headers.ContentType,
                cancellation: ct
            );
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException("File content was not found.");
        }
    }
}