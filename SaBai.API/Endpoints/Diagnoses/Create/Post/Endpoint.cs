using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Diagnoses.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/diagnoses");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Add a diagnosis to a consult");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        _ = await db.Consults.FirstOrNotFoundAsync(
            x => x.Id == req.ConsultId,
            "Consult was not found.",
            ct
        );
        var diagnosis = new Diagnosis
        {
            ConsultId = req.ConsultId,
            Details = req.Details.Trim(),
            Category = req.Category,
        };
        db.Diagnoses.Add(diagnosis);
        await db.SaveChangesAsync(ct);

        var response = new Response(
            diagnosis.Id,
            diagnosis.ConsultId,
            diagnosis.Details,
            diagnosis.Category
        );
        await Send.CreatedAtAsync<Get.Endpoint>(new { diagnosis.Id }, response, cancellation: ct);
    }
}
