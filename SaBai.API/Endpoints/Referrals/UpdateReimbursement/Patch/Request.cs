namespace SaBai.API.Endpoints.Referrals.UpdateReimbursement.Patch;

public sealed record Request(
    Guid Id,
    bool IsReimbursed,
    string? ReimbursementNotes,
    decimal? ReimbursedUsd = null,
    int? ReimbursedRiels = null
);
