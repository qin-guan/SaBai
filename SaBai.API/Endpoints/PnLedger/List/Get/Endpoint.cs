using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.PnLedger.List.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/pn-ledger");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "List per-PN balances and double-entry ledger entries");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rawEntries = await db
            .PnLedgerEntries.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.MemberId,
                MemberName = x.Member.Nickname ?? x.Member.UserName ?? "Staff",
                x.EntryType,
                x.DebitAccount,
                x.CreditAccount,
                x.AmountUsd,
                x.AmountRiels,
                x.ReferralId,
                PatientName = x.Referral != null ? x.Referral.Consult.Visit.Patient.Name : null,
                x.ReversedEntryId,
                x.Notes,
                x.CreatedAt,
                x.CreatedByUserId,
                CreatedByName = x.CreatedByUser != null
                    ? x.CreatedByUser.Nickname ?? x.CreatedByUser.UserName
                    : null,
            })
            .ToListAsync(ct);

        var reversedIds = rawEntries
            .Where(x => x.ReversedEntryId.HasValue)
            .Select(x => x.ReversedEntryId!.Value)
            .ToHashSet();

        var entries = rawEntries
            .Select(x => new PnLedgerEntryResponse(
                x.Id,
                x.MemberId,
                x.MemberName,
                x.EntryType,
                x.DebitAccount,
                x.CreditAccount,
                x.AmountUsd,
                x.AmountRiels,
                x.ReferralId,
                x.PatientName,
                x.ReversedEntryId,
                reversedIds.Contains(x.Id),
                x.Notes,
                x.CreatedAt,
                x.CreatedByUserId,
                x.CreatedByName
            ))
            .ToList();

        var balances = rawEntries
            .GroupBy(x => new { x.MemberId, x.MemberName })
            .Select(g =>
            {
                var billsUsd = g.Where(x => x.EntryType == PnLedgerEntryType.Bill).Sum(x => x.AmountUsd);
                var billsRiels = g.Where(x => x.EntryType == PnLedgerEntryType.Bill).Sum(x => x.AmountRiels);
                var repaidUsd = -g.Where(x => x.EntryType == PnLedgerEntryType.Repayment).Sum(x => x.AmountUsd);
                var repaidRiels = -g.Where(x => x.EntryType == PnLedgerEntryType.Repayment).Sum(x => x.AmountRiels);
                var adjUsd = g.Where(x => x.EntryType == PnLedgerEntryType.Adjustment).Sum(x => x.AmountUsd);
                var adjRiels = g.Where(x => x.EntryType == PnLedgerEntryType.Adjustment).Sum(x => x.AmountRiels);
                var balanceUsd = g.Sum(x => x.AmountUsd);
                var balanceRiels = g.Sum(x => x.AmountRiels);
                return new PnMemberBalanceResponse(
                    g.Key.MemberId,
                    g.Key.MemberName,
                    billsUsd,
                    billsRiels,
                    repaidUsd,
                    repaidRiels,
                    adjUsd,
                    adjRiels,
                    balanceUsd,
                    balanceRiels,
                    g.Count(),
                    g.Max(x => x.CreatedAt)
                );
            })
            .OrderByDescending(x => x.BalanceUsd != 0m || x.BalanceRiels != 0)
            .ThenBy(x => x.MemberName)
            .ToList();

        await Send.OkAsync(new Response(balances, entries), ct);
    }
}
