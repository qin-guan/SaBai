using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using Domain = SaBai.API.Domain;

namespace SaBai.API.Endpoints.PatientConsult.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/patient-consult");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Load the consult workspace for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.VisitId < 1)
        {
            throw new BadRequestException("visitId must be a positive integer.");
        }

        var visit = await db
            .Visits.AsNoTracking()
            .Include(x => x.Patient)
                .ThenInclude(x => x.VillageCode)
            .Include(x => x.Vitals)
            .FirstOrNotFoundAsync(x => x.Id == req.VisitId, "Visit was not found.", ct);

        var consults = await db
            .Consults.AsNoTracking()
            .Where(x => x.VisitId == visit.Id)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Id)
            .Select(x => new ConsultItem(x.Id, x.Date, x.Doctor.Nickname, x.ReferredFor))
            .ToListAsync(ct);

        var prescriptions = await db
            .Orders.AsNoTracking()
            .Where(x => x.Consult.VisitId == visit.Id)
            .OrderBy(x => x.Consult.Date)
            .ThenBy(x => x.Id)
            .Select(x => new PrescriptionItem(
                x.ConsultId,
                x.Consult.Visit.Date,
                x.MedicationBrand.Name,
                x.Quantity,
                x.Remarks,
                x.Status
            ))
            .ToListAsync(ct);

        var lastVisit = await db
            .Visits.AsNoTracking()
            .Where(x => x.PatientId == visit.PatientId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new { x.Id, x.Date })
            .FirstOrDefaultAsync(ct);

        await Send.OkAsync(
            new Response(
                MapPatient(visit.Patient, lastVisit?.Date, lastVisit?.Id),
                MapVitals(visit.Vitals),
                visit.Date,
                consults,
                prescriptions
            ),
            ct
        );
    }

    private static PatientItem MapPatient(
        Patient patient,
        DateTimeOffset? lastVisitDate,
        int? lastVisitId
    )
    {
        var code = patient.VillageCode?.Code;
        return new PatientItem(
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
            code,
            Patient.FormatDisplayId(code, patient.Id),
            Patient.FormatFilterString(code, patient.Id, patient.ContactNo, patient.Name),
            lastVisitDate,
            lastVisitId
        );
    }

    private static VitalsItem? MapVitals(Domain.Vitals? vitals) =>
        vitals is null
            ? null
            : new VitalsItem(
                vitals.Id,
                vitals.VisitId,
                vitals.Height,
                vitals.Weight,
                vitals.Temperature,
                vitals.Systolic,
                vitals.Diastolic,
                vitals.HeartRate,
                vitals.HemocueCount,
                vitals.DiabetesMellitus,
                vitals.UrineTest,
                vitals.BloodGlucoseNonFasting,
                vitals.BloodGlucoseFasting,
                vitals.Hba1c,
                vitals.Others
            );
}
