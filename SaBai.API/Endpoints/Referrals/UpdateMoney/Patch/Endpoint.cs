using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.UpdateMoney.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Get.Response>
{
    public override void Configure()
    {
        Patch("/referrals/{Id}/money");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
            s.Summary = "Update only the referral money fields, preventing overwrites of booth or party sections"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var referral = await db.Referrals.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Referral was not found.",
            ct
        );

        if (req.MemberWhoPaidId is Guid memberId
            && !await db.Users.AnyAsync(x => x.Id == memberId, ct))
        {
            throw new NotFoundException("Staff member who paid was not found.");
        }

        referral.AmountPaidUsd = req.AmountPaidUsd;
        referral.AmountPaidRiels = req.AmountPaidRiels;
        referral.MemberWhoPaidId = req.MemberWhoPaidId;

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
                x.MemberWhoPaidId
            ))
            .FirstAsync(ct);

        await Send.OkAsync(updated, ct);
    }
}
