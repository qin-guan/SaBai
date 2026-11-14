namespace SaBai.API.Endpoints.PnLedger.Repay.Post;

public sealed record Request(
    Guid MemberId,
    decimal? AmountUsd,
    int? AmountRiels,
    string? Notes
);
