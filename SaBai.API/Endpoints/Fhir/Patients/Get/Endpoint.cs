using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Fhir;

namespace SaBai.API.Endpoints.Fhir.Patients.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/fhir/r4/Patient/{Id}");
        Roles(
            StaffRoles.Admin,
            StaffRoles.Member,
            StaffRoles.Doctor,
            StaffRoles.Nurse,
            StaffRoles.Pharmacist,
            StaffRoles.Dentist,
            StaffRoles.Optometrist,
            StaffRoles.Registration
        );
        Summary(s => s.Summary = "Read a Patient resource in HL7 FHIR R4 format");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var patient = await db
            .Patients.AsNoTracking()
            .Include(x => x.VillageCode)
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);

        var fhirPatient = FhirMapper.ToFhirPatient(patient);
        await Send.StringAsync(
            FhirMapper.Serialize(fhirPatient),
            StatusCodes.Status200OK,
            FhirMapper.FhirJsonContentType,
            ct
        );
    }
}
