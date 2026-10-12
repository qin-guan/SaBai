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
        await Send.CreatedAtAsync<Get.Endpoint>(new { Id = response.Id }, response, cancellation: ct);
    }

    private async Task<Response> RegisterAsync(Request req, CancellationToken ct)
    {
        var village = await db.VillageCodes.FirstOrNotFoundAsync(
                x => x.Id == req.VillageCodeId,
                "Village code was not found.",
                ct);
        var now = DateTimeOffset.UtcNow;
        var patient = new Patient
        {
            Name = req.Name.Trim(),
            IdentificationNumber = Normalize(req.IdentificationNumber),
            ContactNo = Normalize(req.ContactNo),
            Gender = req.Gender ?? throw new BadRequestException("Gender is required."),
            DateOfBirth = req.DateOfBirth ?? throw new BadRequestException("Date of birth is required."),
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
            Patient = patient,
            VillageCodeId = village.Id,
            Date = now,
            Status = Visit.StartedStatus,
        };
        if (req.Vitals is not null)
        {
            visit.Vitals = ToVitals(req.Vitals);
        }

        patient.Visits.Add(visit);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);

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
                Patient.FormatDisplayId(village.Code, patient.Id),
                Patient.FormatFilterString(village.Code, patient.Id, patient.ContactNo, patient.Name),
                visit.Date,
                visit.Id,
                visit.Id,
                visit.Vitals?.Id,
                false,
                false);
    }

    private static Domain.Vitals ToVitals(VitalsRequest request) => new()
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
