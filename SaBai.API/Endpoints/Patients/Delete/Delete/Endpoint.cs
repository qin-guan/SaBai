using Amazon.S3;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.FaceRecognition;
using SaBai.API.Storage;

namespace SaBai.API.Endpoints.Patients.Delete.Delete;

/// <summary>
/// Permanently deletes a patient together with every record that hangs off them:
/// visits (and their vitals, eyesight and dental rows), consults (diagnoses, referrals,
/// orders), clinical files, the profile photo and enrolled faces. Stock changes from
/// approved orders are kept so the stock log still matches the quantities on hand.
/// </summary>
public sealed class Endpoint(
    AppDbContext db,
    IAmazonS3 s3,
    RustFsSettings storage,
    FaceStore store,
    ILogger<Endpoint> logger
) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/patients/{Id}");
        Roles(StaffRoles.Admin);
        Summary(s =>
        {
            s.Summary = "Permanently delete a patient and all of their records";
            s.Description =
                "Admin only. Removes the patient's visits, consults, orders, files, photo and faces.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        string? photoKey = null;
        string? faceId = null;
        List<string> fileKeys = [];

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var patient = await db
                .Patients.AsNoTracking()
                .FirstOrNotFoundAsync(x => x.Id == req.Id, "Patient was not found.", ct);
            photoKey = patient.PhotoStoredFileName;
            faceId = patient.LatestFaceId;
            fileKeys = await db
                .ClinicalFiles.Where(x => x.PatientId == patient.Id)
                .Select(x => x.StoredFileName)
                .ToListAsync(ct);

            var visits = db.Visits.Where(x => x.PatientId == patient.Id);
            var consults = db.Consults.Where(x => visits.Any(v => v.Id == x.VisitId));

            // Restrict-delete relationships first; the rest cascade in the database
            // (order items, diagnoses, vitals/eyesight/dental, files).
            await db.Orders.Where(x => consults.Any(c => c.Id == x.ConsultId)).ExecuteDeleteAsync(ct);
            await db.Referrals.Where(x => consults.Any(c => c.Id == x.ConsultId)).ExecuteDeleteAsync(ct);
            await consults.ExecuteDeleteAsync(ct);
            await visits.ExecuteDeleteAsync(ct);
            await db.Patients.Where(x => x.Id == patient.Id).ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        });

        // Object storage and the face store are cleaned up after the commit so a failure
        // there can never leave a half-deleted patient. Orphaned faces are harmless:
        // face search only returns patients that still exist.
        await PatientPhoto.DeleteObjectAsync(s3, storage, photoKey, logger, ct);
        foreach (var key in fileKeys)
        {
            await PatientPhoto.DeleteObjectAsync(s3, storage, key, logger, ct);
        }

        if (faceId is not null)
        {
            try
            {
                await store.DeletePersonAsync(PatientFaceIdentity.ForPatient(req.Id), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete faces for patient {PatientId}", req.Id);
            }
        }

        await Send.NoContentAsync(ct);
    }
}
