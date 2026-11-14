namespace SaBai.API.Endpoints.Referrals.RepayPn.Post;

public sealed record Response(
    int UpdatedCount,
    decimal AppliedUsd,
    int AppliedRiels,
    DateTimeOffset ReimbursedAt,
    Guid ReimbursedByUserId
);
