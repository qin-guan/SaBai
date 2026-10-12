using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Vision.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/vision");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get the vision composite for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.VisitId <= 0)
        {
            throw new BadRequestException("visitId is required and must be a positive integer.");
        }

        var visit = await db
            .Visits.AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.VillageCode)
            .Include(x => x.Eyesight)
            .FirstOrNotFoundAsync(x => x.Id == req.VisitId, "Visit was not found.", ct);

        var eyesight = visit.Eyesight;
        await Send.OkAsync(
            new Response(
                new PatientResponse(
                    visit.Patient.Id,
                    visit.Patient.Name,
                    Patient.FormatDisplayId(visit.VillageCode.Code, visit.Patient.Id),
                    visit.Patient.Gender,
                    visit.Patient.DateOfBirth
                ),
                eyesight is null
                    ? null
                    : new GlassesResponse(
                        eyesight.Id,
                        eyesight.VisitId,
                        eyesight.LeftPrescribedGlassesDegree,
                        eyesight.RightPrescribedGlassesDegree,
                        eyesight.Comments
                    ),
                eyesight is null
                    ? null
                    : new AcuityResponse(
                        eyesight.LeftEyeDegree,
                        eyesight.RightEyeDegree,
                        eyesight.LeftEyePinhole,
                        eyesight.RightEyePinhole
                    )
            ),
            ct
        );
    }
}
