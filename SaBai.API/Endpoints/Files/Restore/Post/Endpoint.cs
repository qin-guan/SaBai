using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Files.Restore.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/files/{Id}/restore");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Restore a soft-deleted clinical file");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var file = await db.ClinicalFiles.FirstOrNotFoundAsync(x => x.Id == req.Id, "File was not found.", ct);
        file.IsDeleted = false;
        await db.SaveChangesAsync(ct);
        await Send.OkAsync(
            new Response(file.Id, file.PatientId, file.FileName, file.StoredFileName, file.Description, file.SizeBytes, file.IsDeleted, file.CreatedAt),
            ct);
    }
}
