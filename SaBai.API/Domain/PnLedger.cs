namespace SaBai.API.Domain;

public enum PnLedgerEntryType
{
    Bill = 0,
    Repayment = 1,
    Adjustment = 2,
}

/// <summary>
/// Immutable double-entry ledger row for PN referral expenses and repayments.
/// Positive <see cref="AmountUsd"/> / <see cref="AmountRiels"/> increases the balance owed to the PN;
/// negative values decrease the balance owed (e.g. repayments or downward bill adjustments).
/// </summary>
public sealed class PnLedgerEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MemberId { get; set; }

    public StaffUser Member { get; set; } = null!;

    public PnLedgerEntryType EntryType { get; set; }

    public required string DebitAccount { get; set; }

    public required string CreditAccount { get; set; }

    public decimal AmountUsd { get; set; }

    public int AmountRiels { get; set; }

    public Guid? ReferralId { get; set; }

    public Referral? Referral { get; set; }

    public Guid? ReversedEntryId { get; set; }

    public PnLedgerEntry? ReversedEntry { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? CreatedByUserId { get; set; }

    public StaffUser? CreatedByUser { get; set; }
}
