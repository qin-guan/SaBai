using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Vitals.UpdateByVisit.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/visits/{VisitId}/vitals");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update vitals for a visit");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.Vitals.FirstOrNotFoundAsync(
            x => x.VisitId == req.VisitId,
            "Vitals were not found.",
            ct
        );

        if (req.Height is { } height)
        {
            entity.Height = height;
        }

        if (req.Weight is { } weight)
        {
            entity.Weight = weight;
        }

        if (req.Temperature is { } temperature)
        {
            entity.Temperature = temperature;
        }

        if (req.Systolic is { } systolic)
        {
            entity.Systolic = systolic;
        }

        if (req.Diastolic is { } diastolic)
        {
            entity.Diastolic = diastolic;
        }

        if (req.HeartRate is { } heartRate)
        {
            entity.HeartRate = heartRate;
        }

        if (req.HemocueCount is { } hemocueCount)
        {
            entity.HemocueCount = hemocueCount;
        }

        if (req.DiabetesMellitus is { } diabetesMellitus)
        {
            entity.DiabetesMellitus = diabetesMellitus;
        }

        if (req.UrineTest is not null)
        {
            entity.UrineTest = FreeText.Normalize(req.UrineTest);
        }

        if (req.BloodGlucoseNonFasting is { } bloodGlucoseNonFasting)
        {
            entity.BloodGlucoseNonFasting = bloodGlucoseNonFasting;
        }

        if (req.BloodGlucoseFasting is { } bloodGlucoseFasting)
        {
            entity.BloodGlucoseFasting = bloodGlucoseFasting;
        }

        if (req.Hba1c is { } hba1c)
        {
            entity.Hba1c = hba1c;
        }

        if (req.Others is not null)
        {
            entity.Others = FreeText.Normalize(req.Others);
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(Response.From(entity), ct);
    }
}
