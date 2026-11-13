namespace SaBai.API.Endpoints.Referrals.UpdateMoney.Patch;

public sealed record Request(
    int Id,
    decimal? AmountPaidUsd,
    int? AmountPaidRiels,
    Guid? MemberWhoPaidId
);
