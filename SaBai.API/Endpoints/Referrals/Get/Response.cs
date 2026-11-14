using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.Get;

/// <summary>
/// Referral with consult, doctor nickname, patient, and the village of the visit it was made at.
/// </summary>
public sealed record Response(
    Guid Id,
    string ReferredFor,
    string? ReferralNotes,
    ReferralState ReferralState,
    string? ReferralOutcome,
    Guid ConsultId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ConsultDate,
    Guid DoctorId,
    Guid PatientId,
    string PatientName,
    DateTimeOffset PatientDateOfBirth,
    string? PatientContactNo,
    bool PatientHasPoorCard,
    bool PatientHasBS2Card,
    bool PatientHasSabaiCard,
    bool HasPhoto,
    Guid VillageCodeId,
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
    Guid? MemberWhoPaidId,
    bool IsReimbursed,
    DateTimeOffset? ReimbursedAt,
    Guid? ReimbursedByUserId,
    string? ReimbursementNotes
);
