using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Disclosures.Post;

public sealed class Endpoint(AppDbContext db, AuditTrail audit, TimeProvider clock)
    : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/patients/{PatientId}/disclosures");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Doctor);
        Summary(s => s.Summary = "Record an external PHI disclosure for HIPAA §164.528 accounting");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db
            .Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.PatientId, "Patient was not found.", ct);

        var disclosure = new DisclosureLog
        {
            PatientId = req.PatientId,
            Recipient = req.Recipient,
            Purpose = req.Purpose,
            Description = req.Description,
            DisclosedAt = clock.GetUtcNow(),
            DisclosedByUserId = User.Id(),
        };

        db.DisclosureLogs.Add(disclosure);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditAction.Disclosed,
            details: $"Recipient={req.Recipient}; Purpose={req.Purpose}",
            entityType: nameof(Patient),
            entityId: req.PatientId.ToString(),
            ct: ct
        );

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { PatientId = req.PatientId },
            new Response(
                disclosure.Id,
                disclosure.PatientId,
                disclosure.Recipient,
                disclosure.Purpose,
                disclosure.DisclosedAt
            ),
            cancellation: ct
        );
    }
}

public sealed record Request(int PatientId, string Recipient, string Purpose, string? Description);

public sealed record Response(
    int Id,
    int PatientId,
    string Recipient,
    string Purpose,
    DateTimeOffset DisclosedAt
);
