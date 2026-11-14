using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Sync.Patients.Merge.Post;

public sealed record Request(Guid SourcePatientId, Guid TargetPatientId);

public sealed record Response(Guid TargetPatientId, int MovedVisits, int MovedFiles);

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/sync/patients/merge");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Merge a duplicate patient record into a target patient");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.SourcePatientId == Guid.Empty || req.TargetPatientId == Guid.Empty)
        {
            ThrowError("SourcePatientId and TargetPatientId are required.");
        }
        if (req.SourcePatientId == req.TargetPatientId)
        {
            ThrowError("Cannot merge a patient into itself.");
        }

        var source = await db.Patients.FirstOrDefaultAsync(x => x.Id == req.SourcePatientId, ct);
        var target = await db.Patients.FirstOrDefaultAsync(x => x.Id == req.TargetPatientId, ct);
        if (source is null || target is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await using var tx = db.Database.IsNpgsql()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var visits = await db.Visits.Where(v => v.PatientId == source.Id).ToListAsync(ct);
        foreach (var v in visits)
        {
            v.PatientId = target.Id;
        }

        var files = await db.Files.Where(f => f.PatientId == source.Id).ToListAsync(ct);
        foreach (var f in files)
        {
            f.PatientId = target.Id;
        }

        var consents = await db.PatientConsents.Where(c => c.PatientId == source.Id).ToListAsync(ct);
        foreach (var c in consents)
        {
            c.PatientId = target.Id;
        }

        var disclosures = await db.DisclosureLogs.Where(d => d.PatientId == source.Id).ToListAsync(ct);
        foreach (var d in disclosures)
        {
            d.PatientId = target.Id;
        }

        var targetTagIds = (
            await db.PatientTags.Where(pt => pt.PatientId == target.Id).Select(pt => pt.TagId).ToListAsync(ct)
        ).ToHashSet();
        var sourceTags = await db.PatientTags.Where(pt => pt.PatientId == source.Id).ToListAsync(ct);
        foreach (var pt in sourceTags)
        {
            if (targetTagIds.Add(pt.TagId))
                pt.PatientId = target.Id;
            else
                db.PatientTags.Remove(pt);
        }

        // Fill missing profile fields on target from source if target is blank.
        if (string.IsNullOrWhiteSpace(target.IdentificationNumber) && !string.IsNullOrWhiteSpace(source.IdentificationNumber))
            target.IdentificationNumber = source.IdentificationNumber;
        if (string.IsNullOrWhiteSpace(target.ContactNo) && !string.IsNullOrWhiteSpace(source.ContactNo))
            target.ContactNo = source.ContactNo;
        if (target.VillageCodeId is null && source.VillageCodeId is not null)
            target.VillageCodeId = source.VillageCodeId;

        db.Patients.Remove(source);
        await db.SaveChangesAsync(ct);

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        await Send.OkAsync(new Response(target.Id, visits.Count, files.Count), ct);
    }
}
