using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Patients.Consents.Post;

public sealed class Endpoint(AppDbContext db, FaceStore faces, TimeProvider clock)
    : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/patients/{PatientId}/consents");
        Roles(
            StaffRoles.Admin,
            StaffRoles.Member,
            StaffRoles.Doctor,
            StaffRoles.Nurse,
            StaffRoles.Registration
        );
        Summary(s => s.Summary = "Record or revoke a HIPAA or biometric consent for a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db
            .Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.PatientId, "Patient was not found.", ct);

        var consent = await db.PatientConsents.FindExistingAsync(
            req.Id,
            x => x.PatientId == req.PatientId,
            ct
        );
        if (consent is null)
        {
            var now = clock.GetUtcNow();
            consent = new PatientConsent
            {
                Id = req.Id ?? Guid.CreateVersion7(),
                PatientId = req.PatientId,
                ConsentType = req.ConsentType,
                Granted = req.Granted,
                RecordedAt = now,
                RevokedAt = req.Granted ? null : now,
                RecordedByUserId = User.Id(),
                Notes = req.Notes,
            };

            db.PatientConsents.Add(consent);
            await db.SaveChangesAsync(ct);
        }

        // Withdrawing face recognition consent deletes the patient's enrolled faces so they stop
        // matching. This also runs when a withdrawal is resent, finishing a deletion that failed,
        // unless consent has been granted again since.
        if (
            consent is { ConsentType: ConsentType.BiometricFaceRecognition, Granted: false }
            && !await BiometricConsent.IsGrantedAsync(db, req.PatientId, ct)
        )
        {
            await faces.DeletePersonAsync(PatientFaceIdentity.ForPatient(req.PatientId), ct);
            // A bulk update: clearing the face reference is not an edit of the patient's details,
            // so it neither bumps UpdatedAt nor trips an offline replay's If-Unmodified-Since.
            await db
                .Patients.Where(x => x.Id == req.PatientId && x.LatestFaceId != null)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.LatestFaceId, (string?)null), ct);
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { PatientId = req.PatientId },
            new Response(
                consent.Id,
                consent.PatientId,
                consent.ConsentType,
                consent.Granted,
                consent.RecordedAt,
                consent.RevokedAt
            ),
            cancellation: ct
        );
    }
}
