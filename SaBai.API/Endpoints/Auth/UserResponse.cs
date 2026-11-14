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
        AppDbContext db,
        CancellationToken ct = default
    ) => From(user, await users.GetRolesAsync(user), await TagSync.LoadStaffUserTagsAsync(db, user.Id, ct));

    /// <summary>Loads users with their roles and tags in bulk queries instead of per user.</summary>
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
        var tags = await TagSync.LoadStaffUserTagsBulkAsync(db, ids, ct);

        return users.Select(x => From(x, roles[x.Id], tags[x.Id])).ToList();
    }
}
