namespace SaBai.API.Endpoints.PnLedger.Repay.Post;

public sealed record Response(
    Guid Id,
    Guid MemberId,
    decimal BalanceUsd,
    int BalanceRiels,
    DateTimeOffset CreatedAt
);
