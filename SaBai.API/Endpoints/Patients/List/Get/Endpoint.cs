using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/patients");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List patients");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.Patients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            var name = req.Name.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(req.VillageCode))
        {
            var code = req.VillageCode.Trim().ToLower();
            query = query.Where(x => x.VillageCode != null && x.VillageCode.Code.ToLower() == code);
        }

        var rows = await query
            .OrderBy(x => x.Id)
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
            .ToListAsync(ct);
        var latest = await LatestVisitsAsync([.. rows.Select(x => x.Id)], ct);

        var patients = rows
            .Select(x =>
            {
                latest.TryGetValue(x.Id, out var last);
                return new Item(
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
                    x.VillageCode,
                    Patient.FormatDisplayId(x.VillageCode, x.Id),
                    Patient.FormatFilterString(x.VillageCode, x.Id, x.ContactNo, x.Name),
                    last?.Date,
                    last?.Id,
                    x.PhotoStoredFileName != null,
                    x.LatestFaceId != null);
            })
            .ToList();

        await Send.OkAsync(new Response(patients), ct);
    }

    private async Task<Dictionary<int, LatestVisit>> LatestVisitsAsync(int[] patientIds, CancellationToken ct)
    {
        if (patientIds.Length == 0)
        {
            return [];
        }

        var visits = await db.Visits.AsNoTracking()
            .Where(x => patientIds.Contains(x.PatientId))
            .Select(x => new LatestVisit(x.Id, x.PatientId, x.Date))
            .ToListAsync(ct);

        return visits
            .GroupBy(x => x.PatientId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).First());
    }

    private sealed record LatestVisit(int Id, int PatientId, DateTimeOffset Date);
}
