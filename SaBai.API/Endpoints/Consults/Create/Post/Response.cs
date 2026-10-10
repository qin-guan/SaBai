using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Consults.Create.Post;

/// <summary>Consult recorded by the current doctor, including its diagnoses.</summary>
public sealed record Response(
    int Id,
    int VisitId,
    DateTimeOffset Date,
    string? PastMedicalHistory,
    string? Consultation,
    string? TreatmentPlan,
    string? ReferredFor,
    string? ReferralNotes,
    string? Remarks,
    Guid DoctorId,
    string DoctorNickname,
    IReadOnlyList<DiagnosisItem> Diagnoses)
{
    public static Response From(Consult consult) =>
        new(
            consult.Id,
            consult.VisitId,
            consult.Date,
            consult.PastMedicalHistory,
            consult.Consultation,
            consult.TreatmentPlan,
            consult.ReferredFor,
            consult.ReferralNotes,
            consult.Remarks,
            consult.DoctorId,
            consult.Doctor.Nickname,
            consult.Diagnoses
                .OrderBy(x => x.Id)
                .Select(x => new DiagnosisItem(x.Id, x.ConsultId, x.Details, x.Category))
                .ToList());
}

/// <summary>Diagnosis recorded on a consult.</summary>
public sealed record DiagnosisItem(int Id, int ConsultId, string Details, DiagnosisCategory? Category);
