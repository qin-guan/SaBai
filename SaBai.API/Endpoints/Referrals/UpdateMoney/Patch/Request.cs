namespace SaBai.API.Endpoints.Referrals.UpdateMoney.Patch;

public sealed record Request(
    Guid Id,
    decimal? AmountPaidUsd,
    int? AmountPaidRiels,
    Guid? MemberWhoPaidId
);
