using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Referrals.Create.Post;

/// <summary>Referral just opened, including the consult's doctor and patient.</summary>
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
    string DoctorNickname
);
