using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.PnLedger.Repay.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/pn-ledger/repayments");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s =>
            s.Summary = "Record a repayment to a PN member (supports underpayment and overpayment)"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == req.MemberId, ct))
        {
            throw new NotFoundException("Staff member was not found.");
        }

        var usd = req.AmountUsd ?? 0m;
        var riels = req.AmountRiels ?? 0;
        var now = DateTimeOffset.UtcNow;

        var entry = new PnLedgerEntry
        {
            MemberId = req.MemberId,
            EntryType = PnLedgerEntryType.Repayment,
            DebitAccount = "PN:Payable",
            CreditAccount = "Clinic:Cash",
            AmountUsd = -usd,
            AmountRiels = -riels,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
            CreatedAt = now,
            CreatedByUserId = User.Id(),
        };

        db.PnLedgerEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        var balanceUsd = await db
            .PnLedgerEntries.Where(x => x.MemberId == req.MemberId)
            .SumAsync(x => x.AmountUsd, ct);
        var balanceRiels = await db
            .PnLedgerEntries.Where(x => x.MemberId == req.MemberId)
            .SumAsync(x => x.AmountRiels, ct);

        await Send.OkAsync(
            new Response(entry.Id, req.MemberId, balanceUsd, balanceRiels, now),
            ct
        );
    }
}
