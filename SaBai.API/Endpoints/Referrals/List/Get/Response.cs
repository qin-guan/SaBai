using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.List.Get;

/// <summary>
/// Referral with the consult date, doctor, patient, and the village of the visit it was made at.
/// </summary>
public sealed record Response(
    Guid Id,
    string ReferredFor,
    string? ReferralNotes,
    ReferralState ReferralState,
    string? ReferralOutcome,
    Guid ConsultId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ConsultDate,
    Guid DoctorId,
    Guid PatientId,
    string PatientName,
    bool HasPhoto,
    Guid VillageCodeId,
    string VillageCode,
    string VillageColorHex
);
