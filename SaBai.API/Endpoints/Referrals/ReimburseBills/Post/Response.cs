namespace SaBai.API.Endpoints.Referrals.ReimburseBills.Post;

public sealed record Response(
    int UpdatedCount,
    bool IsReimbursed,
    DateTimeOffset? ReimbursedAt,
    Guid? ReimbursedByUserId,
    string? ReimbursementNotes
);
