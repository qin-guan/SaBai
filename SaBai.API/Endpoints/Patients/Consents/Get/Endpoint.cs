using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients.Consents.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/patients/{PatientId}/consents");
        Roles(
            StaffRoles.Admin,
            StaffRoles.Member,
            StaffRoles.Doctor,
            StaffRoles.Nurse,
            StaffRoles.Registration,
            StaffRoles.Auditor
        );
        Summary(s => s.Summary = "List HIPAA and biometric consents for a patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db
            .Patients.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.PatientId, "Patient was not found.", ct);

        var items = await db
            .PatientConsents.AsNoTracking()
            .Where(x => x.PatientId == req.PatientId)
            .OrderByDescending(x => x.RecordedAt)
            .Select(x => new Item(
                x.Id,
                x.ConsentType,
                x.Granted,
                x.RecordedAt,
                x.RevokedAt,
                x.RecordedByUserId,
                x.Notes
            ))
            .ToListAsync(ct);

        await Send.OkAsync(new Response(items), ct);
    }
}

public sealed record Request(int PatientId);

public sealed record Item(
    int Id,
    ConsentType ConsentType,
    bool Granted,
    DateTimeOffset RecordedAt,
    DateTimeOffset? RevokedAt,
    Guid RecordedByUserId,
    string? Notes
);

public sealed record Response(IReadOnlyList<Item> Consents);
