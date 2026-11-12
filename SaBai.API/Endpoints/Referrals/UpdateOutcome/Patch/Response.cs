using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.UpdateOutcome.Patch;

/// <summary>Referral after an outcome change, including doctor nickname and patient.</summary>
public sealed record Response(
    int Id,
    string ReferredFor,
    string? ReferralNotes,
    ReferralState ReferralState,
    string? ReferralOutcome,
    int ConsultId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ConsultDate,
    Guid DoctorId,
    int PatientId,
    string PatientName,
    string DoctorNickname
);
