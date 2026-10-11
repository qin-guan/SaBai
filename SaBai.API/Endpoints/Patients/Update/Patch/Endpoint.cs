using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/patients/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db.Patients.FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);

        if (req.Name is not null)
        {
            patient.Name = req.Name.Trim();
        }

        if (req.IdentificationNumber is not null)
        {
            var identificationNumber = req.IdentificationNumber.Trim();
            if (identificationNumber.Length > 0 &&
                await db.Patients.AnyAsync(x => x.Id != patient.Id && x.IdentificationNumber == identificationNumber, ct))
            {
                throw new ConflictException("Another patient already has this identification number.");
            }

            patient.IdentificationNumber = identificationNumber.Length == 0 ? null : identificationNumber;
        }

        if (req.ContactNo is not null)
        {
            patient.ContactNo = string.IsNullOrWhiteSpace(req.ContactNo) ? null : req.ContactNo.Trim();
        }

        if (req.Gender is Gender gender)
        {
            patient.Gender = gender;
        }

        if (req.DateOfBirth is DateTimeOffset dateOfBirth)
        {
            patient.DateOfBirth = dateOfBirth;
        }

        if (req.DrugAllergy is not null)
        {
            patient.DrugAllergy = req.DrugAllergy.Trim();
        }

        if (req.HasPoorCard is bool hasPoorCard)
        {
            patient.HasPoorCard = hasPoorCard;
        }

        if (req.HasBS2Card is bool hasBS2Card)
        {
            patient.HasBS2Card = hasBS2Card;
        }

        if (req.HasSabaiCard is bool hasSabaiCard)
        {
            patient.HasSabaiCard = hasSabaiCard;
        }

        if (req.ToGetReport is bool toGetReport)
        {
            patient.ToGetReport = toGetReport;
        }

        if (req.VillageCodeId is int villageCodeId)
        {
            await db.VillageCodes.AsNoTracking().FirstOrNotFoundAsync(
                x => x.Id == villageCodeId,
                "Village code was not found.",
                ct);
            patient.VillageCodeId = villageCodeId;
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(await LoadAsync(patient.Id, ct), ct);
    }

    private async Task<Response> LoadAsync(int id, CancellationToken ct)
    {
        await db.Patients.AsNoTracking().FirstOrNotFoundAsync(x => x.Id == id, "Patient was not found.", ct);
        var row = await db.Patients.AsNoTracking()
            .Where(x => x.Id == id)
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
        var lastVisit = await db.Visits.AsNoTracking()
            .Where(x => x.PatientId == row.Id)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Select(x => new { x.Id, x.Date })
            .FirstOrDefaultAsync(ct);

        return new Response(
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
            row.LatestFaceId != null);
    }
}
