using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.PnLedger.Reverse.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/pn-ledger/entries/{Id}/reverse");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "Reverse a PN repayment or adjustment ledger entry");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var original = await db.PnLedgerEntries.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Ledger entry was not found.",
            ct
        );

        if (original.EntryType == PnLedgerEntryType.Bill)
        {
            ThrowError("Referral bill entries are adjusted by editing the referral payment.");
        }

        if (await db.PnLedgerEntries.AnyAsync(x => x.ReversedEntryId == original.Id, ct))
        {
            ThrowError("This ledger entry has already been reversed.");
        }

        var now = DateTimeOffset.UtcNow;
        var notes = string.IsNullOrWhiteSpace(req.Notes)
            ? $"Reversal of {original.EntryType}"
            : req.Notes.Trim();

        var reversal = new PnLedgerEntry
        {
            MemberId = original.MemberId,
            EntryType = PnLedgerEntryType.Adjustment,
            DebitAccount = original.CreditAccount,
            CreditAccount = original.DebitAccount,
            AmountUsd = -original.AmountUsd,
            AmountRiels = -original.AmountRiels,
            ReversedEntryId = original.Id,
            Notes = notes,
            CreatedAt = now,
            CreatedByUserId = User.Id(),
        };

        db.PnLedgerEntries.Add(reversal);
        await db.SaveChangesAsync(ct);

        var balanceUsd = await db
            .PnLedgerEntries.Where(x => x.MemberId == original.MemberId)
            .SumAsync(x => x.AmountUsd, ct);
        var balanceRiels = await db
            .PnLedgerEntries.Where(x => x.MemberId == original.MemberId)
            .SumAsync(x => x.AmountRiels, ct);

        await Send.OkAsync(
            new Response(
                reversal.Id,
                original.Id,
                original.MemberId,
                balanceUsd,
                balanceRiels,
                now
            ),
            ct
        );
    }
}
