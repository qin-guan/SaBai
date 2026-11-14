namespace SaBai.API.Endpoints.Referrals.RepayPn.Post;

public sealed record Request(
    Guid MemberWhoPaidId,
    decimal? AmountUsd,
    int? AmountRiels,
    string? ReimbursementNotes
);
