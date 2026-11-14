using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Fhir;

namespace SaBai.API.Endpoints.Fhir.Patients.Everything;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/fhir/r4/Patient/{Id}/$everything");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Doctor, StaffRoles.Nurse);
        Summary(s => s.Summary = "Export a patient's complete HL7 FHIR R4 clinical summary Bundle");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db
            .Patients.AsNoTracking()
            .Include(x => x.VillageCode)
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);

        var visits = await db
            .Visits.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.VillageCode)
            .Include(x => x.Vitals)
            .Include(x => x.Consults)
                .ThenInclude(c => c.Diagnoses)
            .Include(x => x.Consults)
                .ThenInclude(c => c.Referrals)
            .Include(x => x.Consults)
                .ThenInclude(c => c.Orders)
                    .ThenInclude(o => o.MedicationBrand)
                        .ThenInclude(b => b.ActiveIngredient)
            .Where(x => x.PatientId == req.Id)
            .OrderByDescending(x => x.Date)
            .ToListAsync(ct);

        var files = await db
            .Files.AsNoTracking()
            .Where(x => x.PatientId == req.Id && !x.IsDeleted)
            .ToListAsync(ct);

        var bundle = FhirMapper.BuildPatientEverythingBundle(patient, visits, files);

        await Send.StringAsync(
            FhirMapper.Serialize(bundle),
            StatusCodes.Status200OK,
            FhirMapper.FhirJsonContentType,
            ct
        );
    }
}
