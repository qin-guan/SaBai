using Microsoft.EntityFrameworkCore;
using SaBai.API.Domain;

namespace SaBai.API.Data;

/// <summary>
/// Helpers for normalizing, resolving, syncing, and querying freeform tags on staff users and patients.
/// </summary>
public static class TagSync
{
    public const int MaxTagLength = 64;

    public static List<string> NormalizeNames(IEnumerable<string>? rawTags)
    {
        if (rawTags is null)
        {
            return [];
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in rawTags)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (trimmed.Length > MaxTagLength)
            {
                throw new BadRequestException($"Tag '{trimmed[..20]}...' exceeds {MaxTagLength} characters.");
            }

            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    public static async Task<List<Tag>> ResolveTagsAsync(
        AppDbContext db,
        IReadOnlyList<string> names,
        CancellationToken ct
    )
    {
        if (names.Count == 0)
        {
            return [];
        }

        var normalized = names.Select(x => x.ToUpperInvariant()).ToList();
        var existing = await db
            .Tags.Where(x => normalized.Contains(x.NormalizedName))
            .ToDictionaryAsync(x => x.NormalizedName, StringComparer.Ordinal, ct);

        var resolved = new List<Tag>(names.Count);
        foreach (var name in names)
        {
            var key = name.ToUpperInvariant();
            if (!existing.TryGetValue(key, out var tag))
            {
                tag = new Tag
                {
                    Name = name,
                    NormalizedName = key,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.Tags.Add(tag);
                existing[key] = tag;
            }

            resolved.Add(tag);
        }

        return resolved;
    }

    public static async Task SyncStaffUserTagsAsync(
        AppDbContext db,
        Guid staffUserId,
        IEnumerable<string>? rawTags,
        CancellationToken ct
    )
    {
        var names = NormalizeNames(rawTags);
        var wantedTags = await ResolveTagsAsync(db, names, ct);

        var existing = await db
            .StaffUserTags.Where(x => x.StaffUserId == staffUserId)
            .ToListAsync(ct);

        var wantedExistingIds = wantedTags.Where(t => db.Entry(t).State != EntityState.Added).Select(t => t.Id).ToHashSet();
        var toRemove = existing.Where(x => !wantedExistingIds.Contains(x.TagId)).ToList();
        if (toRemove.Count > 0)
        {
            db.StaffUserTags.RemoveRange(toRemove);
        }

        var existingIds = existing.Select(x => x.TagId).ToHashSet();
        foreach (var tag in wantedTags)
        {
            if (db.Entry(tag).State == EntityState.Added || !existingIds.Contains(tag.Id))
            {
                db.StaffUserTags.Add(new StaffUserTag { StaffUserId = staffUserId, Tag = tag });
            }
        }
    }

    public static async Task SyncPatientTagsAsync(
        AppDbContext db,
        Patient patient,
        IEnumerable<string>? rawTags,
        CancellationToken ct
    )
    {
        var names = NormalizeNames(rawTags);
        var wantedTags = await ResolveTagsAsync(db, names, ct);

        if (db.Entry(patient).State is EntityState.Added or EntityState.Detached)
        {
            foreach (var tag in wantedTags)
            {
                patient.PatientTags.Add(new PatientTag { Patient = patient, Tag = tag });
            }

            return;
        }

        var existing = await db
            .PatientTags.Where(x => x.PatientId == patient.Id)
            .ToListAsync(ct);

        var wantedExistingIds = wantedTags.Where(t => db.Entry(t).State != EntityState.Added).Select(t => t.Id).ToHashSet();
        var toRemove = existing.Where(x => !wantedExistingIds.Contains(x.TagId)).ToList();
        if (toRemove.Count > 0)
        {
            db.PatientTags.RemoveRange(toRemove);
        }

        var existingIds = existing.Select(x => x.TagId).ToHashSet();
        var added = false;
        foreach (var tag in wantedTags)
        {
            if (db.Entry(tag).State == EntityState.Added || !existingIds.Contains(tag.Id))
            {
                db.PatientTags.Add(new PatientTag { PatientId = patient.Id, Tag = tag });
                added = true;
            }
        }

        // Tag links are not tracked records themselves: a changed tag set counts as a change
        // to the patient, so it is checked against If-Unmodified-Since and stamped.
        if (added || toRemove.Count > 0)
        {
            db.Entry(patient).Property(x => x.UpdatedAt).IsModified = true;
        }
    }

    public static async Task<IReadOnlyList<string>> LoadStaffUserTagsAsync(
        AppDbContext db,
        Guid staffUserId,
        CancellationToken ct
    ) =>
        await db
            .StaffUserTags.AsNoTracking()
            .Where(x => x.StaffUserId == staffUserId)
            .OrderBy(x => x.Tag.Name)
            .Select(x => x.Tag.Name)
            .ToListAsync(ct);

    public static async Task<ILookup<Guid, string>> LoadStaffUserTagsBulkAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> staffUserIds,
        CancellationToken ct
    )
    {
        if (staffUserIds.Count == 0)
        {
            return Enumerable.Empty<KeyValuePair<Guid, string>>().ToLookup(x => x.Key, x => x.Value);
        }

        var rows = await db
            .StaffUserTags.AsNoTracking()
            .Where(x => staffUserIds.Contains(x.StaffUserId))
            .OrderBy(x => x.Tag.Name)
            .Select(x => new { x.StaffUserId, x.Tag.Name })
            .ToListAsync(ct);

        return rows.ToLookup(x => x.StaffUserId, x => x.Name);
    }

    public static async Task<IReadOnlyList<string>> LoadPatientTagsAsync(
        AppDbContext db,
        Guid patientId,
        CancellationToken ct
    ) =>
        await db
            .PatientTags.AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderBy(x => x.Tag.Name)
            .Select(x => x.Tag.Name)
            .ToListAsync(ct);

    public static async Task<ILookup<Guid, string>> LoadPatientTagsBulkAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct
    )
    {
        if (patientIds.Count == 0)
        {
            return Enumerable.Empty<KeyValuePair<Guid, string>>().ToLookup(x => x.Key, x => x.Value);
        }

        var rows = await db
            .PatientTags.AsNoTracking()
            .Where(x => patientIds.Contains(x.PatientId))
            .OrderBy(x => x.Tag.Name)
            .Select(x => new { x.PatientId, x.Tag.Name })
            .ToListAsync(ct);

        return rows.ToLookup(x => x.PatientId, x => x.Name);
    }
}
