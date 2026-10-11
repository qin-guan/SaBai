using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Consults.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/consults/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update consult text within 24 hours");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var consult = await db.Consults
            .Include(x => x.Doctor)
            .Include(x => x.Diagnoses)
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Consult was not found.", ct);

        if (consult.IsEditLocked(DateTimeOffset.UtcNow))
        {
            throw new LockedException("Cannot edit consult after 24 hours");
        }

        if (req.PastMedicalHistory is not null)
        {
            consult.PastMedicalHistory = req.PastMedicalHistory;
        }

        if (req.Consultation is not null)
        {
            consult.Consultation = req.Consultation;
        }

        if (req.TreatmentPlan is not null)
        {
            consult.TreatmentPlan = req.TreatmentPlan;
        }

        if (req.ReferredFor is not null)
        {
            consult.ReferredFor = req.ReferredFor;
        }

        if (req.ReferralNotes is not null)
        {
            consult.ReferralNotes = req.ReferralNotes;
        }

        if (req.Remarks is not null)
        {
            consult.Remarks = req.Remarks;
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(Response.From(consult), ct);
    }
}
