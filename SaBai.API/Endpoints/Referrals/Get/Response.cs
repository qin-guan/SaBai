using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.Get;

/// <summary>
/// Referral with consult, doctor nickname, patient, and the village of the visit it was made at.
/// </summary>
public sealed record Response(
    int Id,
    string ReferredFor,
    string? ReferralNotes,
    ReferralState ReferralState,
    string? ReferralOutcome,
    int ConsultId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ConsultDate,
    Guid DoctorId,
    int PatientId,
    string PatientName,
    DateTimeOffset PatientDateOfBirth,
    string? PatientContactNo,
    bool PatientHasPoorCard,
    bool PatientHasBS2Card,
    bool PatientHasSabaiCard,
    bool HasPhoto,
    int VillageCodeId,
    string VillageCode,
    string VillageColorHex,
    string DoctorNickname,
    // Referral booth section
    Guid? ReferredToId,
    Guid? MemberInChargeId,
    GuardiansContacted? GuardiansContacted,
    ReferralServiceClass? ClassOfService,
    string? DescriptionOfService,
    string? PatientSummaryFromScreening,
    string? RemarksAtBooth,
    // Referral party section
    Guid? MemberOnRunId,
    bool PresentAtReferralParty,
    string? ReferralPartyResults,
    bool FollowUp,
    bool DoctorsUpdated,
    // Money section
    decimal? AmountPaidUsd,
    int? AmountPaidRiels,
    Guid? MemberWhoPaidId
);
