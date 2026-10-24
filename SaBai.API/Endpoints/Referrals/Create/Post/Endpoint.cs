using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/referrals");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Open a referral for a consult");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        _ = await db.Consults.FirstOrNotFoundAsync(
            x => x.Id == req.ConsultId,
            "Consult was not found.",
            ct
        );
        var referral = new Referral
        {
            ReferredFor = req.ReferredFor.Trim(),
            ReferralNotes = req.ReferralNotes,
            ReferralState = ReferralState.New,
            ReferralOutcome = null,
            ConsultId = req.ConsultId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Referrals.Add(referral);
        await db.SaveChangesAsync(ct);

        var created = await db
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

        await Send.CreatedAtAsync<Get.Endpoint>(new { referral.Id }, created, cancellation: ct);
    }
}
