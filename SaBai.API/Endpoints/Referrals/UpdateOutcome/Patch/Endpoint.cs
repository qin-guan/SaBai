using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Referrals.UpdateOutcome.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/referrals/{Id}/outcome");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
            s.Summary = "Set only a referral's outcome, so it can't overwrite a newer state"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // Only the outcome is marked modified, so EF writes just that column and a state change
        // made in the meantime is never overwritten. Going through SaveChanges keeps the audit log.
        var referral = await db.Referrals.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Referral was not found.",
            ct
        );
        referral.ReferralOutcome = req.ReferralOutcome;
        await db.SaveChangesAsync(ct);

        var updated = await db
            .Referrals.AsNoTracking()
            .Where(x => x.Id == referral.Id)
            .Select(x => new Response(
                x.Id,
                x.ReferredFor,
                x.ReferralNotes,
                x.ReferralState,
                x.ReferralOutcome,
                x.ConsultId,
                x.CreatedAt,
                x.Consult.Date,
                x.Consult.DoctorId,
                x.Consult.Visit.PatientId,
                x.Consult.Visit.Patient.Name,
                x.Consult.Doctor.Nickname
            ))
            .FirstAsync(ct);

        await Send.OkAsync(updated, ct);
    }
}
