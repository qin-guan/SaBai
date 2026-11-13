using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth;

/// <summary>Staff profile returned by authentication and user endpoints.</summary>
/// <param name="IsLocked">True while the account cannot sign in.</param>
/// <param name="LockoutEnd">
/// When the lock ends. Far in the future (year 9999) when an admin locked the account;
/// a few minutes ahead after too many failed sign-ins.
/// </param>
/// <param name="HasPassword">False for accounts that still need a password set by an admin.</param>
public sealed record UserResponse(
    Guid Id,
    string UserName,
    string? Email,
    string Nickname,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Tags,
    bool IsLocked,
    DateTimeOffset? LockoutEnd,
    bool TwoFactorEnabled,
    bool HasPassword,
    DateTimeOffset CreatedAt
)
{
    public static UserResponse From(
        StaffUser user,
        IEnumerable<string> roles,
        IEnumerable<string>? tags = null
    ) =>
        new(
            user.Id,
            user.UserName ?? "",
            user.Email,
            user.Nickname,
            roles.Order(StringComparer.Ordinal).ToList(),
            (tags ?? []).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            user.IsLockedOut(DateTimeOffset.UtcNow),
            user.IsLockedOut(DateTimeOffset.UtcNow) ? user.LockoutEnd : null,
            user.TwoFactorEnabled,
            user.PasswordHash is not null,
            user.CreatedAt
        );

    public static async Task<UserResponse> FromAsync(
        StaffUser user,
        UserManager<StaffUser> users,
        IEnumerable<string>? tags = null
    ) => From(user, await users.GetRolesAsync(user), tags);

    public static async Task<UserResponse> FromAsync(
        StaffUser user,
        UserManager<StaffUser> users,
        AppDbContext db,
        CancellationToken ct
    )
    {
        var tags = await db
            .StaffUserTags.AsNoTracking()
            .Where(x => x.StaffUserId == user.Id)
            .Select(x => x.Tag.Name)
            .ToListAsync(ct);
        return From(user, await users.GetRolesAsync(user), tags);
    }

    public static async Task<List<string>> SyncTagsAsync(
        AppDbContext db,
        Guid userId,
        IEnumerable<string> requestedTags,
        CancellationToken ct
    )
    {
        var desired = requestedTags
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var desiredUpper = desired.Select(t => t.ToUpperInvariant()).ToHashSet();

        var existingTags = await db.Tags.ToListAsync(ct);
        var byUpper = existingTags.ToDictionary(t => t.Name.ToUpperInvariant(), t => t);

        var targetTags = new List<Tag>(desired.Count);
        foreach (var name in desired)
        {
            if (byUpper.TryGetValue(name.ToUpperInvariant(), out var existing))
            {
                targetTags.Add(existing);
            }
            else
            {
                var created = new Tag { Name = name };
                db.Tags.Add(created);
                targetTags.Add(created);
                byUpper[name.ToUpperInvariant()] = created;
            }
        }

        // Flush new Tag entities so their integer Ids are populated before linking.
        await db.SaveChangesAsync(ct);

        var currentLinks = await db
            .StaffUserTags.Where(x => x.StaffUserId == userId)
            .ToListAsync(ct);
        var targetIds = targetTags.Select(t => t.Id).ToHashSet();

        var toRemove = currentLinks.Where(x => !targetIds.Contains(x.TagId)).ToList();
        if (toRemove.Count > 0)
        {
            db.StaffUserTags.RemoveRange(toRemove);
        }

        var currentIds = currentLinks.Select(x => x.TagId).ToHashSet();
        foreach (var tagId in targetIds.Where(id => !currentIds.Contains(id)))
        {
            db.StaffUserTags.Add(new StaffUserTag { StaffUserId = userId, TagId = tagId });
        }

        await db.SaveChangesAsync(ct);
        return targetTags.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Loads users with their roles and tags in batched queries instead of one per user.</summary>
    public static async Task<List<UserResponse>> ListAsync(
        AppDbContext db,
        IQueryable<StaffUser> query,
        CancellationToken ct
    )
    {
        var users = await query.AsNoTracking().ToListAsync(ct);
        var ids = users.Select(x => x.Id).ToList();
        var roles = (
            await (
                from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                where ids.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name }
            ).ToListAsync(ct)
        ).ToLookup(x => x.UserId, x => x.Name!);

        var tags = (
            await (
                from userTag in db.StaffUserTags
                join tag in db.Tags on userTag.TagId equals tag.Id
                where ids.Contains(userTag.StaffUserId)
                select new { userTag.StaffUserId, tag.Name }
            ).ToListAsync(ct)
        ).ToLookup(x => x.StaffUserId, x => x.Name);

        return users.Select(x => From(x, roles[x.Id], tags[x.Id])).ToList();
    }
}
