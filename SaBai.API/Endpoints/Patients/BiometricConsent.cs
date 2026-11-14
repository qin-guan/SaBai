using Microsoft.EntityFrameworkCore;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients;

/// <summary>
/// Whether a patient currently consents to face recognition. Consents are an append-only history:
/// the latest <see cref="ConsentType.BiometricFaceRecognition"/> record decides, and it counts only
/// while it is granted and not revoked. No record means no consent.
/// </summary>
public static class BiometricConsent
{
    public const string MissingMessage =
        "Face recognition consent has not been granted for this patient. Record the patient's biometric consent before enrolling a face.";

    public static async Task<bool> IsGrantedAsync(
        AppDbContext db,
        Guid patientId,
        CancellationToken ct
    ) => (await GrantedAsync(db, [patientId], ct)).Contains(patientId);

    /// <summary>The subset of <paramref name="patientIds"/> whose consent is current.</summary>
    public static async Task<IReadOnlySet<Guid>> GrantedAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct
    )
    {
        if (patientIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = patientIds.ToArray();
        var records = await db
            .PatientConsents.AsNoTracking()
            .Where(x =>
                ids.Contains(x.PatientId)
                && x.ConsentType == ConsentType.BiometricFaceRecognition
            )
            .Select(x => new
            {
                x.Id,
                x.PatientId,
                x.Granted,
                x.RecordedAt,
                x.RevokedAt,
            })
            .ToListAsync(ct);

        return records
            .GroupBy(x => x.PatientId)
            .Select(g => g.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).First())
            .Where(x => x.Granted && x.RevokedAt is null)
            .Select(x => x.PatientId)
            .ToHashSet();
    }
}
