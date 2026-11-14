using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.WithLatestVisit.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/patients/with-latest-visit");
        Options(options =>
        {
            options.WithOrder(-1);
        });
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "List patients with their latest visit");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await db
            .Patients.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Number,
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
                VillageColorHex = x.VillageCode != null ? x.VillageCode.ColorHex : null,
                x.PhotoStoredFileName,
                x.LatestFaceId,
            })
            .ToListAsync(ct);
        var patientIds = rows.Select(x => x.Id).ToArray();
        var latest = await LatestVisitsAsync(patientIds, ct);
        var journeys = await VisitJourney.ForVisitsAsync(
            db.Visits,
            [.. latest.Values.Select(x => x.Id)],
            ct
        );
        var tagsByPatient = await TagSync.LoadPatientTagsBulkAsync(db, patientIds, ct);

        var patients = rows.Select(x =>
            {
                latest.TryGetValue(x.Id, out var last);
                return new { Row = x, Last = last };
            })
            .OrderByDescending(x => x.Last is not null)
            .ThenByDescending(x => x.Last?.Date)
            .ThenBy(x => x.Row.Id)
            .Select(x => new Item(
                x.Row.Id,
                x.Row.Name,
                x.Row.IdentificationNumber,
                x.Row.ContactNo,
                x.Row.Gender,
                x.Row.DateOfBirth,
                x.Row.DrugAllergy,
                x.Row.HasPoorCard,
                x.Row.HasBS2Card,
                x.Row.HasSabaiCard,
                x.Row.ToGetReport,
                x.Row.VillageCodeId,
                x.Row.VillageCode,
                x.Row.VillageColorHex,
                Patient.FormatDisplayId(x.Row.VillageCode, x.Row.Number),
                Patient.FormatFilterString(
                    x.Row.VillageCode,
                    x.Row.Number,
                    x.Row.ContactNo,
                    x.Row.Name
                ),
                x.Last?.Date,
                x.Last?.Id,
                x.Row.PhotoStoredFileName != null,
                x.Row.LatestFaceId != null,
                x.Last is not null ? journeys.GetValueOrDefault(x.Last.Id) : null,
                tagsByPatient[x.Row.Id].ToList()
            ))
            .ToList();

        await Send.OkAsync(new Response(patients), ct);
    }

    private async Task<Dictionary<Guid, LatestVisit>> LatestVisitsAsync(
        Guid[] patientIds,
        CancellationToken ct
    )
    {
        if (patientIds.Length == 0)
        {
            return [];
        }

        var visits = await db
            .Visits.AsNoTracking()
            .Where(x => patientIds.Contains(x.PatientId))
            .Select(x => new LatestVisit(x.Id, x.PatientId, x.Date))
            .ToListAsync(ct);

        return visits
            .GroupBy(x => x.PatientId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).First()
            );
    }

    private sealed record LatestVisit(Guid Id, Guid PatientId, DateTimeOffset Date);
}
