using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Patients.Disclosures.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/patients/{PatientId}/disclosures");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Doctor, StaffRoles.Auditor);
        Summary(s => s.Summary = "List HIPAA Accounting of Disclosures for a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db
            .Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.PatientId, "Patient was not found.", ct);

        var items = await db
            .DisclosureLogs.AsNoTracking()
            .Where(x => x.PatientId == req.PatientId)
            .OrderByDescending(x => x.DisclosedAt)
            .Select(x => new Item(
                x.Id,
                x.Recipient,
                x.Purpose,
                x.Description,
                x.DisclosedAt,
                x.DisclosedByUserId
            ))
            .ToListAsync(ct);

        await Send.OkAsync(new Response(items), ct);
    }
}
