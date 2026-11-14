namespace SaBai.API.Endpoints.Referrals.ReimburseBills.Post;

public sealed record Request(
    IReadOnlyList<Guid> ReferralIds,
    bool IsReimbursed,
    string? ReimbursementNotes
);
