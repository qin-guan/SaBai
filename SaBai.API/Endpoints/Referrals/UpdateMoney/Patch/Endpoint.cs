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
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
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

        var oldMemberId = referral.MemberWhoPaidId;
        var oldUsd = referral.AmountPaidUsd ?? 0m;
        var oldRiels = referral.AmountPaidRiels ?? 0;

        referral.AmountPaidUsd = req.AmountPaidUsd;
        referral.AmountPaidRiels = req.AmountPaidRiels;
        referral.MemberWhoPaidId = req.MemberWhoPaidId;

        var newMemberId = referral.MemberWhoPaidId;
        var newUsd = referral.AmountPaidUsd ?? 0m;
        var newRiels = referral.AmountPaidRiels ?? 0;
        var userId = User.Id();
        var now = DateTimeOffset.UtcNow;

        if (oldMemberId == newMemberId)
        {
            if (newMemberId is Guid sameMember)
            {
                var deltaUsd = newUsd - oldUsd;
                var deltaRiels = newRiels - oldRiels;
                if (deltaUsd != 0m || deltaRiels != 0)
                {
                    AddBillLedgerEntries(
                        db,
                        sameMember,
                        referral.Id,
                        deltaUsd,
                        deltaRiels,
                        oldUsd == 0m && oldRiels == 0
                            ? "Referral bill recorded"
                            : "Referral bill updated",
                        userId,
                        now
                    );
                }
            }
        }
        else
        {
            if (oldMemberId is Guid prevMember && (oldUsd != 0m || oldRiels != 0))
            {
                AddBillLedgerEntries(
                    db,
                    prevMember,
                    referral.Id,
                    -oldUsd,
                    -oldRiels,
                    "Referral payer reassigned",
                    userId,
                    now
                );
            }
            if (newMemberId is Guid nextMember && (newUsd != 0m || newRiels != 0))
            {
                AddBillLedgerEntries(
                    db,
                    nextMember,
                    referral.Id,
                    newUsd,
                    newRiels,
                    "Referral bill recorded",
                    userId,
                    now
                );
            }
        }

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

    private static void AddBillLedgerEntries(
        AppDbContext db,
        Guid memberId,
        Guid referralId,
        decimal usd,
        int riels,
        string notes,
        Guid createdByUserId,
        DateTimeOffset createdAt
    )
    {
        if (usd != 0m && riels != 0 && Math.Sign(usd) != Math.Sign(riels))
        {
            db.PnLedgerEntries.Add(new PnLedgerEntry
            {
                MemberId = memberId,
                EntryType = PnLedgerEntryType.Bill,
                DebitAccount = usd > 0m ? "Clinic:ReferralExpense" : "PN:Payable",
                CreditAccount = usd > 0m ? "PN:Payable" : "Clinic:ReferralExpense",
                AmountUsd = usd,
                AmountRiels = 0,
                ReferralId = referralId,
                Notes = notes,
                CreatedAt = createdAt,
                CreatedByUserId = createdByUserId,
            });
            db.PnLedgerEntries.Add(new PnLedgerEntry
            {
                MemberId = memberId,
                EntryType = PnLedgerEntryType.Bill,
                DebitAccount = riels > 0 ? "Clinic:ReferralExpense" : "PN:Payable",
                CreditAccount = riels > 0 ? "PN:Payable" : "Clinic:ReferralExpense",
                AmountUsd = 0m,
                AmountRiels = riels,
                ReferralId = referralId,
                Notes = notes,
                CreatedAt = createdAt,
                CreatedByUserId = createdByUserId,
            });
            return;
        }

        var isCreditToPn = usd > 0m || riels > 0;
        db.PnLedgerEntries.Add(new PnLedgerEntry
        {
            MemberId = memberId,
            EntryType = PnLedgerEntryType.Bill,
            DebitAccount = isCreditToPn ? "Clinic:ReferralExpense" : "PN:Payable",
            CreditAccount = isCreditToPn ? "PN:Payable" : "Clinic:ReferralExpense",
            AmountUsd = usd,
            AmountRiels = riels,
            ReferralId = referralId,
            Notes = notes,
            CreatedAt = createdAt,
            CreatedByUserId = createdByUserId,
        });
    }
}
