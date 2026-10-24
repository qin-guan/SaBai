using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Files.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Delete("/files/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Soft-delete a clinical file");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var file = await db.ClinicalFiles.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "File was not found.",
            ct
        );
        file.IsDeleted = true;
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
