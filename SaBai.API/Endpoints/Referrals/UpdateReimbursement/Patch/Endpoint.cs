using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Referrals.UpdateReimbursement.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Get.Response>
{
    public override void Configure()
    {
        Patch("/referrals/{Id}/reimbursement");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s =>
            s.Summary = "Mark a referral bill as reimbursed or pending reimbursement"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var referral = await db.Referrals.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Referral was not found.",
            ct
        );

        referral.IsReimbursed = req.IsReimbursed;
        referral.ReimbursedAt = req.IsReimbursed ? DateTimeOffset.UtcNow : null;
        referral.ReimbursedByUserId = req.IsReimbursed ? User.Id() : null;
        referral.ReimbursementNotes = string.IsNullOrWhiteSpace(req.ReimbursementNotes)
            ? null
            : req.ReimbursementNotes.Trim();

        await db.SaveChangesAsync(ct);

        var updated = await db
            .Referrals.AsNoTracking()
            .Where(x => x.Id == referral.Id)
            .Select(x => new Get.Response(
                x.Id,
                x.ReferredFor,
                x.ReferralNotes,
                x.ReferralState,
                x.ReferralOutcome,
                x.ConsultId,
                x.CreatedAt,
                x.UpdatedAt,
                x.Consult.Date,
                x.Consult.DoctorId,
                x.Consult.Visit.PatientId,
                x.Consult.Visit.Patient.Name,
                x.Consult.Visit.Patient.DateOfBirth,
                x.Consult.Visit.Patient.ContactNo,
                x.Consult.Visit.Patient.HasPoorCard,
                x.Consult.Visit.Patient.HasBS2Card,
                x.Consult.Visit.Patient.HasSabaiCard,
                x.Consult.Visit.Patient.PhotoStoredFileName != null,
                x.Consult.Visit.VillageCodeId,
                x.Consult.Visit.VillageCode.Code,
                x.Consult.Visit.VillageCode.ColorHex,
                x.Consult.Doctor.Nickname,
                x.ReferredToId,
                x.MemberInChargeId,
                x.GuardiansContacted,
                x.ClassOfService,
                x.DescriptionOfService,
                x.PatientSummaryFromScreening,
                x.RemarksAtBooth,
                x.MemberOnRunId,
                x.PresentAtReferralParty,
                x.ReferralPartyResults,
                x.FollowUp,
                x.DoctorsUpdated,
                x.AmountPaidUsd,
                x.AmountPaidRiels,
                x.MemberWhoPaidId,
                x.IsReimbursed,
                x.ReimbursedAt,
                x.ReimbursedByUserId,
                x.ReimbursementNotes
            ))
            .FirstAsync(ct);

        await Send.OkAsync(updated, ct);
    }
}
