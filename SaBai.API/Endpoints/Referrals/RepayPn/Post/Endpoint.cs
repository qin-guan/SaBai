using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.RepayPn.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/referrals/bills/repay-pn");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s =>
            s.Summary = "Apply a full or partial lump-sum repayment to a PN member's pending referral bills (FIFO)"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var pending = await db
            .Referrals.Where(x => x.MemberWhoPaidId == req.MemberWhoPaidId && !x.IsReimbursed)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            throw new NotFoundException("No pending referral bills were found for this PN member.");
        }

        var totalOwedUsd = pending.Sum(r => Math.Max(0m, (r.AmountPaidUsd ?? 0m) - (r.ReimbursedUsd ?? 0m)));
        var totalOwedRiels = pending.Sum(r => Math.Max(0, (r.AmountPaidRiels ?? 0) - (r.ReimbursedRiels ?? 0)));

        var toApplyUsd = req.AmountUsd ?? 0m;
        var toApplyRiels = req.AmountRiels ?? 0;

        if (toApplyUsd > totalOwedUsd)
        {
            ThrowError($"Repayment in USD ({toApplyUsd:0.00}) exceeds total owed ({totalOwedUsd:0.00}).");
        }
        if (toApplyRiels > totalOwedRiels)
        {
            ThrowError($"Repayment in KHR ({toApplyRiels}) exceeds total owed ({totalOwedRiels}).");
        }

        var remainingUsd = toApplyUsd;
        var remainingRiels = toApplyRiels;
        var now = DateTimeOffset.UtcNow;
        var userId = User.Id();
        var notes = string.IsNullOrWhiteSpace(req.ReimbursementNotes)
            ? null
            : req.ReimbursementNotes.Trim();
        var updatedCount = 0;

        foreach (var referral in pending)
        {
            var paidUsd = referral.AmountPaidUsd ?? 0m;
            var paidRiels = referral.AmountPaidRiels ?? 0;
            var currentUsd = referral.ReimbursedUsd ?? 0m;
            var currentRiels = referral.ReimbursedRiels ?? 0;

            var owedUsd = Math.Max(0m, paidUsd - currentUsd);
            var owedRiels = Math.Max(0, paidRiels - currentRiels);

            var applyUsd = Math.Min(owedUsd, remainingUsd);
            var applyRiels = Math.Min(owedRiels, remainingRiels);

            if (applyUsd <= 0m && applyRiels <= 0)
                continue;

            var nextUsd = currentUsd + applyUsd;
            var nextRiels = currentRiels + applyRiels;

            referral.ReimbursedUsd = nextUsd > 0m ? nextUsd : null;
            referral.ReimbursedRiels = nextRiels > 0 ? nextRiels : null;
            referral.IsReimbursed = nextUsd >= paidUsd && nextRiels >= paidRiels;
            referral.ReimbursedAt = now;
            referral.ReimbursedByUserId = userId;
            if (notes is not null)
                referral.ReimbursementNotes = notes;

            remainingUsd -= applyUsd;
            remainingRiels -= applyRiels;
            updatedCount++;
        }

        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new Response(
                updatedCount,
                toApplyUsd - remainingUsd,
                toApplyRiels - remainingRiels,
                now,
                userId
            ),
            ct
        );
    }
}
