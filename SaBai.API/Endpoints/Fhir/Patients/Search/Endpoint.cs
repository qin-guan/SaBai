using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Fhir;
using FhirBundle = Hl7.Fhir.Model.Bundle;

namespace SaBai.API.Endpoints.Fhir.Patients.Search;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Get("/fhir/r4/Patient");
        Roles(
            StaffRoles.Admin,
            StaffRoles.Member,
            StaffRoles.Doctor,
            StaffRoles.Nurse,
            StaffRoles.Registration
        );
        Summary(s => s.Summary = "Search Patient resources in HL7 FHIR R4 format");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.Patients.AsNoTracking().Include(x => x.VillageCode).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            var term = req.Name.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(req.Identifier))
        {
            var idTerm = req.Identifier.Trim();
            query = query.Where(x => x.IdentificationNumber == idTerm);
        }

        var limit = Math.Clamp(req.Count ?? 20, 1, 100);
        var matches = await query.OrderBy(x => x.Id).Take(limit).ToListAsync(ct);

        var bundle = new FhirBundle
        {
            Type = FhirBundle.BundleType.Searchset,
            Total = matches.Count,
            Timestamp = DateTimeOffset.UtcNow,
            Entry =
            [
                .. matches.Select(p => new FhirBundle.EntryComponent
                {
                    FullUrl = $"Patient/{p.Id}",
                    Resource = FhirMapper.ToFhirPatient(p),
                }),
            ],
        };

        await Send.StringAsync(
            FhirMapper.Serialize(bundle),
            StatusCodes.Status200OK,
            FhirMapper.FhirJsonContentType,
            ct
        );
    }
}
