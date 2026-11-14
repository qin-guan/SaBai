using SaBai.API.Domain;

namespace SaBai.API.Endpoints.PnLedger.List.Get;

public sealed record PnMemberBalanceResponse(
    Guid MemberId,
    string MemberName,
    decimal TotalBillsUsd,
    int TotalBillsRiels,
    decimal TotalRepaidUsd,
    int TotalRepaidRiels,
    decimal TotalAdjustmentsUsd,
    int TotalAdjustmentsRiels,
    decimal BalanceUsd,
    int BalanceRiels,
    int EntryCount,
    DateTimeOffset? LastActivityAt
);

public sealed record PnLedgerEntryResponse(
    Guid Id,
    Guid MemberId,
    string MemberName,
    PnLedgerEntryType EntryType,
    string DebitAccount,
    string CreditAccount,
    decimal AmountUsd,
    int AmountRiels,
    Guid? ReferralId,
    string? PatientName,
    Guid? ReversedEntryId,
    bool IsReversed,
    string? Notes,
    DateTimeOffset CreatedAt,
    Guid? CreatedByUserId,
    string? CreatedByName
);

public sealed record Response(
    IReadOnlyList<PnMemberBalanceResponse> Balances,
    IReadOnlyList<PnLedgerEntryResponse> Entries
);
