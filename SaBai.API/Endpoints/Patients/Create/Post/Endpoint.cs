using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using Domain = SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/patients");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Register a patient and open a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var response = await RegisterAsync(req, ct);
        await Send.CreatedAtAsync<Get.Endpoint>(
            new { Id = response.Id },
            response,
            cancellation: ct
        );
    }

    private async Task<Response> RegisterAsync(Request req, CancellationToken ct)
    {
        if (await db.Patients.FindExistingAsync(req.Id, ct) is { } existing)
        {
            return await ReplayAsync(existing, req, ct);
        }

        var village = await db.VillageCodes.FirstOrNotFoundAsync(
            x => x.Id == req.VillageCodeId,
            "Village code was not found.",
            ct
        );
        var now = DateTimeOffset.UtcNow;
        var patient = new Patient
        {
            Id = req.Id ?? Guid.CreateVersion7(),
            Name = req.Name.Trim(),
            IdentificationNumber = Normalize(req.IdentificationNumber),
            ContactNo = Normalize(req.ContactNo),
            Gender = req.Gender ?? throw new BadRequestException("Gender is required."),
            DateOfBirth =
                req.DateOfBirth ?? throw new BadRequestException("Date of birth is required."),
            DrugAllergy = req.DrugAllergy.Trim(),
            HasPoorCard = req.HasPoorCard,
            HasBS2Card = req.HasBS2Card,
            HasSabaiCard = req.HasSabaiCard,
            ToGetReport = req.ToGetReport,
            VillageCodeId = village.Id,
            CreatedAt = now,
        };
        var visit = new Visit
        {
            Id = req.VisitId ?? Guid.CreateVersion7(),
            Patient = patient,
            VillageCodeId = village.Id,
            Date = now,
            Status = Visit.StartedStatus,
        };
        if (req.Vitals is not null)
        {
            visit.Vitals = ToVitals(req.Vitals);
            visit.Vitals.Id = req.VitalsId ?? Guid.CreateVersion7();
        }

        patient.Visits.Add(visit);
        if (req.Tags is not null)
        {
            await TagSync.SyncPatientTagsAsync(db, patient, req.Tags, ct);
        }

        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);

        var tags = patient
            .PatientTags.Select(x => x.Tag.Name)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Response(
            patient.Id,
            patient.Name,
            patient.IdentificationNumber,
            patient.ContactNo,
            patient.Gender,
            patient.DateOfBirth,
            patient.DrugAllergy,
            patient.HasPoorCard,
            patient.HasBS2Card,
            patient.HasSabaiCard,
            patient.ToGetReport,
            patient.VillageCodeId,
            village.Code,
            Patient.FormatDisplayId(village.Code, patient.Number),
            Patient.FormatFilterString(village.Code, patient.Number, patient.ContactNo, patient.Name),
            visit.Date,
            visit.Id,
            visit.Id,
            visit.Vitals?.Id,
            false,
            false,
            tags
        );
    }

    /// <summary>
    /// A registration resent with the id of a patient that already exists: answer as the
    /// first registration did, from the rows as they are now, and change nothing.
    /// </summary>
    private async Task<Response> ReplayAsync(Patient patient, Request req, CancellationToken ct)
    {
        var visits = await db
            .Visits.AsNoTracking()
            .Where(x => x.PatientId == patient.Id)
            .OrderBy(x => x.Date)
            .Select(x => new { x.Id, x.Date, VitalsId = (Guid?)x.Vitals!.Id })
            .ToListAsync(ct);
        if (req.VisitId is { } visitId && visits.All(x => x.Id != visitId))
        {
            if (await db.Visits.AnyAsync(x => x.Id == visitId, ct))
            {
                throw new ConflictException($"Id {visitId} is already used by another record.");
            }
        }

        var visit = visits.FirstOrDefault(x => x.Id == req.VisitId) ?? visits.FirstOrDefault();
        var latest = visits.LastOrDefault();
        var villageCode = patient.VillageCodeId is { } villageId
            ? await db
                .VillageCodes.AsNoTracking()
                .Where(x => x.Id == villageId)
                .Select(x => x.Code)
                .FirstOrDefaultAsync(ct)
            : null;
        var tags = await TagSync.LoadPatientTagsAsync(db, patient.Id, ct);

        return new Response(
            patient.Id,
            patient.Name,
            patient.IdentificationNumber,
            patient.ContactNo,
            patient.Gender,
            patient.DateOfBirth,
            patient.DrugAllergy,
            patient.HasPoorCard,
            patient.HasBS2Card,
            patient.HasSabaiCard,
            patient.ToGetReport,
            patient.VillageCodeId,
            villageCode,
            Patient.FormatDisplayId(villageCode, patient.Number),
            Patient.FormatFilterString(villageCode, patient.Number, patient.ContactNo, patient.Name),
            latest?.Date ?? patient.CreatedAt,
            latest?.Id ?? Guid.Empty,
            visit?.Id ?? Guid.Empty,
            visit?.VitalsId,
            patient.PhotoStoredFileName is not null,
            patient.LatestFaceId is not null,
            tags
        );
    }

    private static Domain.Vitals ToVitals(VitalsRequest request) =>
        new()
        {
            Temperature = request.Temperature,
            Height = request.Height,
            Weight = request.Weight,
            Systolic = request.Systolic,
            Diastolic = request.Diastolic,
            HeartRate = request.HeartRate,
            HemocueCount = request.HemocueCount,
            DiabetesMellitus = request.DiabetesMellitus,
            UrineTest = Normalize(request.UrineTest),
            BloodGlucoseNonFasting = request.BloodGlucoseNonFasting,
            BloodGlucoseFasting = request.BloodGlucoseFasting,
            Hba1c = request.Hba1c,
            Others = Normalize(request.Others),
        };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
