using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Files.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/files/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a clinical file");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var file = await db.ClinicalFiles.FirstOrNotFoundAsync(x => x.Id == req.Id, "File was not found.", ct);
        await Send.OkAsync(Response.From(file), ct);
    }
}
