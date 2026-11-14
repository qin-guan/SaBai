using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Referrals.ReimburseBills.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/referrals/bills/reimburse");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s =>
            s.Summary = "Bulk mark referral bills as reimbursed or pending reimbursement"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var ids = req.ReferralIds.Distinct().ToArray();
        var referrals = await db
            .Referrals.Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        if (referrals.Count == 0)
        {
            throw new NotFoundException("No matching referrals were found.");
        }

        var now = req.IsReimbursed ? DateTimeOffset.UtcNow : (DateTimeOffset?)null;
        var userId = req.IsReimbursed ? User.Id() : (Guid?)null;
        var notes = string.IsNullOrWhiteSpace(req.ReimbursementNotes)
            ? null
            : req.ReimbursementNotes.Trim();

        foreach (var referral in referrals)
        {
            referral.IsReimbursed = req.IsReimbursed;
            referral.ReimbursedAt = now;
            referral.ReimbursedByUserId = userId;
            if (req.ReimbursementNotes is not null)
            {
                referral.ReimbursementNotes = notes;
            }
            else if (!req.IsReimbursed)
            {
                referral.ReimbursementNotes = null;
            }
        }

        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new Response(referrals.Count, req.IsReimbursed, now, userId, notes),
            ct
        );
    }
}
