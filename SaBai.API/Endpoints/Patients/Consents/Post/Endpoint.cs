using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Consents.Post;

public sealed class Endpoint(AppDbContext db, TimeProvider clock) : Endpoint<Request, Response>
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

        var now = clock.GetUtcNow();
        var consent = new PatientConsent
        {
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

public sealed record Request(
    int PatientId,
    ConsentType ConsentType,
    bool Granted,
    string? Notes
);

public sealed record Response(
    int Id,
    int PatientId,
    ConsentType ConsentType,
    bool Granted,
    DateTimeOffset RecordedAt,
    DateTimeOffset? RevokedAt
);
