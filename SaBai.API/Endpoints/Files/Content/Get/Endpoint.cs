using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.Storage;

namespace SaBai.API.Endpoints.Files.Content.Get;

public sealed class Endpoint(AppDbContext db, IAmazonS3 s3, RustFsSettings storage)
    : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/files/{Id}/content");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Download a clinical file from RustFS");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var file = await db.Files.FirstOrNotFoundAsync(
            x => x.Id == req.Id && !x.IsDeleted,
            "File was not found.",
            ct
        );

        try
        {
            var obj = await s3.GetObjectAsync(
                new GetObjectRequest { BucketName = storage.Bucket, Key = file.StoredFileName },
                ct
            );

            // Served as an attachment with the type found on upload, never one taken from the
            // client, under a policy that keeps the content from running in the app's origin.
            var contentType = FileSignatures.ContentTypeOfStored(file.StoredFileName);
            HttpContext.Response.Headers.ContentSecurityPolicy =
                FileSignatures.DownloadContentSecurityPolicy;
            await Send.StreamAsync(
                obj.ResponseStream,
                FileSignatures.DownloadName(file.FileName, contentType, "file"),
                file.SizeBytes,
                contentType,
                cancellation: ct
            );
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException("File content was not found.");
        }
    }
}
