namespace SaBai.API.Endpoints.PnLedger.Reverse.Post;

public sealed record Response(
    Guid Id,
    Guid ReversedEntryId,
    Guid MemberId,
    decimal BalanceUsd,
    int BalanceRiels,
    DateTimeOffset CreatedAt
);
