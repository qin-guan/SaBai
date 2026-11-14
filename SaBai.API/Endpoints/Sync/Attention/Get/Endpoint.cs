using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Sync.Attention.Get;

public sealed record ConflictItem(
    Guid Id,
    string Table,
    Guid RecordId,
    string Field,
    string? PreviousValue,
    string? IncomingValue,
    string? WinningValue,
    Guid? IncomingUserId,
    DateTimeOffset OccurredAt
);

public sealed record RejectionItem(
    Guid Id,
    string Table,
    Guid RecordId,
    string Operation,
    string PayloadJson,
    string Reason,
    Guid? UserId,
    DateTimeOffset OccurredAt
);

public sealed record DuplicatePatientSummary(
    Guid Id,
    string DisplayId,
    string Name,
    string? IdentificationNumber,
    DateTimeOffset DateOfBirth,
    string Gender,
    DateTimeOffset CreatedAt,
    int VisitCount
);

public sealed record DuplicatePatientGroup(
    string MatchReason,
    IReadOnlyList<DuplicatePatientSummary> Patients
);

public sealed record Response(
    IReadOnlyList<ConflictItem> Conflicts,
    IReadOnlyList<RejectionItem> Rejections,
    IReadOnlyList<DuplicatePatientGroup> DuplicatePatients
);

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/sync/attention");
        Roles(StaffRoles.Admin, StaffRoles.Member, StaffRoles.Pn);
        Summary(s => s.Summary = "List unresolved sync conflicts, rejections, and potential duplicate patients");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var conflicts = await db
            .SyncConflicts.Where(x => x.ResolvedAt == null)
            .OrderByDescending(x => x.OccurredAt)
            .Take(200)
            .Select(x => new ConflictItem(
                x.Id,
                x.Table,
                x.RecordId,
                x.Field,
                x.PreviousValue,
                x.IncomingValue,
                x.WinningValue,
                x.IncomingUserId,
                x.OccurredAt
            ))
            .ToListAsync(ct);

        var rejections = await db
            .SyncRejections.Where(x => x.ResolvedAt == null)
            .OrderByDescending(x => x.OccurredAt)
            .Take(200)
            .Select(x => new RejectionItem(
                x.Id,
                x.Table,
                x.RecordId,
                x.Operation,
                x.PayloadJson,
                x.Reason,
                x.UserId,
                x.OccurredAt
            ))
            .ToListAsync(ct);

        var patients = await db
            .Patients.Include(x => x.VillageCode)
            .Select(x => new
            {
                x.Id,
                x.Number,
                VillageCode = x.VillageCode != null ? x.VillageCode.Code : null,
                x.Name,
                x.IdentificationNumber,
                x.DateOfBirth,
                Gender = x.Gender.ToString(),
                x.CreatedAt,
                VisitCount = x.Visits.Count,
            })
            .ToListAsync(ct);

        var groups = new List<DuplicatePatientGroup>();
        var seenPairs = new HashSet<string>();

        // 1. Exact IdentificationNumber match (non-empty)
        foreach (
            var grp in patients
                .Where(p => !string.IsNullOrWhiteSpace(p.IdentificationNumber))
                .GroupBy(p => p.IdentificationNumber!.Trim().ToUpperInvariant())
                .Where(g => g.Count() > 1)
        )
        {
            var ids = string.Join(",", grp.Select(p => p.Id).OrderBy(id => id));
            if (seenPairs.Add(ids))
            {
                groups.Add(
                    new DuplicatePatientGroup(
                        $"Same ID number ({grp.Key})",
                        grp.OrderBy(p => p.CreatedAt)
                            .Select(p => new DuplicatePatientSummary(
                                p.Id,
                                Patient.FormatDisplayId(p.VillageCode, p.Number),
                                p.Name,
                                p.IdentificationNumber,
                                p.DateOfBirth,
                                p.Gender,
                                p.CreatedAt,
                                p.VisitCount
                            ))
                            .ToList()
                    )
                );
            }
        }

        // 2. Normalized Name + DateOfBirth (date part)
        foreach (
            var grp in patients
                .GroupBy(p => (NormalizeName(p.Name), p.DateOfBirth.UtcDateTime.Date))
                .Where(g => !string.IsNullOrEmpty(g.Key.Item1) && g.Count() > 1)
        )
        {
            var ids = string.Join(",", grp.Select(p => p.Id).OrderBy(id => id));
            if (seenPairs.Add(ids))
            {
                groups.Add(
                    new DuplicatePatientGroup(
                        $"Same name & date of birth ({grp.Key.Item2:yyyy-MM-dd})",
                        grp.OrderBy(p => p.CreatedAt)
                            .Select(p => new DuplicatePatientSummary(
                                p.Id,
                                Patient.FormatDisplayId(p.VillageCode, p.Number),
                                p.Name,
                                p.IdentificationNumber,
                                p.DateOfBirth,
                                p.Gender,
                                p.CreatedAt,
                                p.VisitCount
                            ))
                            .ToList()
                    )
                );
            }
        }

        await Send.OkAsync(new Response(conflicts, rejections, groups), ct);
    }

    private static string NormalizeName(string name) =>
        string.Join(' ', name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
