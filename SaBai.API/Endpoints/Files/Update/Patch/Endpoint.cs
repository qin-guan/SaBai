using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Files.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/files/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update clinical file metadata");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var file = await db.ClinicalFiles.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "File was not found.",
            ct
        );
        if (req.FileName is not null)
        {
            var fileName = Path.GetFileName(req.FileName.Trim());
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new BadRequestException("No name provided.");
            }

            file.FileName = fileName;
        }

        if (req.Description is not null)
        {
            file.Description = req.Description;
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(
            new Response(
                file.Id,
                file.PatientId,
                file.FileName,
                file.StoredFileName,
                file.Description,
                file.SizeBytes,
                file.IsDeleted,
                file.CreatedAt
            ),
            ct
        );
    }
}
