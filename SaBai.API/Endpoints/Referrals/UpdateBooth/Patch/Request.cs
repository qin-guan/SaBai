using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.UpdateBooth.Patch;

public sealed record Request(
    Guid Id,
    Guid? ReferredToId,
    Guid? MemberInChargeId,
    GuardiansContacted? GuardiansContacted,
    ReferralServiceClass? ClassOfService,
    string? DescriptionOfService,
    string? PatientSummaryFromScreening,
    string? RemarksAtBooth
);
