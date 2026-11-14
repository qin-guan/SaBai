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
            s.Summary = "Update reimbursement status or partial repayment amounts on a referral bill"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var referral = await db.Referrals.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Referral was not found.",
            ct
        );

        var hasPartialAmounts = req.ReimbursedUsd.HasValue || req.ReimbursedRiels.HasValue;

        if (req.IsReimbursed && !hasPartialAmounts)
        {
            referral.IsReimbursed = true;
            referral.ReimbursedUsd = referral.AmountPaidUsd;
            referral.ReimbursedRiels = referral.AmountPaidRiels;
            referral.ReimbursedAt = DateTimeOffset.UtcNow;
            referral.ReimbursedByUserId = User.Id();
        }
        else if (!req.IsReimbursed && !hasPartialAmounts)
        {
            referral.IsReimbursed = false;
            referral.ReimbursedUsd = null;
            referral.ReimbursedRiels = null;
            referral.ReimbursedAt = null;
            referral.ReimbursedByUserId = null;
        }
        else
        {
            var paidUsd = referral.AmountPaidUsd ?? 0m;
            var paidRiels = referral.AmountPaidRiels ?? 0;

            if (req.ReimbursedUsd.HasValue && req.ReimbursedUsd.Value > paidUsd)
                ThrowError("Reimbursed USD cannot exceed the amount paid.");
            if (req.ReimbursedRiels.HasValue && req.ReimbursedRiels.Value > paidRiels)
                ThrowError("Reimbursed KHR cannot exceed the amount paid.");

            var nextUsd = req.ReimbursedUsd ?? referral.ReimbursedUsd ?? 0m;
            var nextRiels = req.ReimbursedRiels ?? referral.ReimbursedRiels ?? 0;

            referral.ReimbursedUsd = nextUsd > 0m ? nextUsd : null;
            referral.ReimbursedRiels = nextRiels > 0 ? nextRiels : null;

            var hasAnyPaid = paidUsd > 0m || paidRiels > 0;
            var fullyRepaid = hasAnyPaid && nextUsd >= paidUsd && nextRiels >= paidRiels;
            referral.IsReimbursed = req.IsReimbursed || fullyRepaid;

            if (referral.IsReimbursed || nextUsd > 0m || nextRiels > 0)
            {
                referral.ReimbursedAt = DateTimeOffset.UtcNow;
                referral.ReimbursedByUserId = User.Id();
            }
            else
            {
                referral.ReimbursedAt = null;
                referral.ReimbursedByUserId = null;
            }
        }

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
                x.ReimbursedUsd,
                x.ReimbursedRiels,
                x.ReimbursedAt,
                x.ReimbursedByUserId,
                x.ReimbursementNotes
            ))
            .FirstAsync(ct);

        await Send.OkAsync(updated, ct);
    }
}
