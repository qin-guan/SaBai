using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/patients/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db
            .Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);
        var row = await db
            .Patients.AsNoTracking()
            .Where(x => x.Id == req.Id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.IdentificationNumber,
                x.ContactNo,
                x.Gender,
                x.DateOfBirth,
                x.DrugAllergy,
                x.HasPoorCard,
                x.HasBS2Card,
                x.HasSabaiCard,
                x.ToGetReport,
                x.VillageCodeId,
                VillageCode = x.VillageCode != null ? x.VillageCode.Code : null,
                x.PhotoStoredFileName,
                x.LatestFaceId,
            })
            .FirstAsync(ct);
        var lastVisit = await db
            .Visits.AsNoTracking()
            .Where(x => x.PatientId == row.Id)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new { x.Id, x.Date })
            .FirstOrDefaultAsync(ct);

        await Send.OkAsync(
            new Response(
                row.Id,
                row.Name,
                row.IdentificationNumber,
                row.ContactNo,
                row.Gender,
                row.DateOfBirth,
                row.DrugAllergy,
                row.HasPoorCard,
                row.HasBS2Card,
                row.HasSabaiCard,
                row.ToGetReport,
                row.VillageCodeId,
                row.VillageCode,
                Patient.FormatDisplayId(row.VillageCode, row.Id),
                Patient.FormatFilterString(row.VillageCode, row.Id, row.ContactNo, row.Name),
                lastVisit?.Date,
                lastVisit?.Id,
                row.PhotoStoredFileName != null,
                row.LatestFaceId != null
            ),
            ct
        );
    }
}
