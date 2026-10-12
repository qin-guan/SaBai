using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.UpdateState.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/referrals/{Id}/state");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Set a referral state from any prior state");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.ReferralState is not { } state)
        {
            throw new BadRequestException("referralState is required.");
        }

        var referral = await db.Referrals.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Referral was not found.",
            ct
        );
        referral.ReferralState = state;
        if (req.ReferralOutcome is not null)
        {
            referral.ReferralOutcome = req.ReferralOutcome;
        }

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
