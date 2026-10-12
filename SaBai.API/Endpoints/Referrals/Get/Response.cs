using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.Get;

/// <summary>Referral with consult, doctor nickname, and patient.</summary>
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
