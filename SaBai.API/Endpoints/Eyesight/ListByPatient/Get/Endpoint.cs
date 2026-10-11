using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Eyesight.ListByPatient.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/patients/{PatientId}/eyesight");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List a patient's eyesight records");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        _ = await db.Patients
            .AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.PatientId, "Patient was not found.", ct);

        var rows = await db.Eyesights
            .AsNoTracking()
            .Where(x => x.Visit.PatientId == req.PatientId)
            .OrderByDescending(x => x.Visit.Date)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        await Send.OkAsync([.. rows.Select(global::SaBai.API.Endpoints.Eyesight.ListByPatient.Get.Response.From)], ct);
    }
}
