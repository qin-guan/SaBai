using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.Import.Importing;

/// <summary>
/// Makes the import act as the "upstream-import" staff account for SaBai's own save hooks.
/// <see cref="SaBai.API.Data.AppDbContext"/> stamps <c>UpdatedByUserId</c> and
/// <see cref="SaBai.API.Data.AuditInterceptor"/> writes audit entries from the current
/// <see cref="HttpContext"/>, which they read through <see cref="HttpContextAccessor"/> (an
/// AsyncLocal). Setting a synthetic context here attributes every imported row and audit entry
/// to the import account, with the request recorded as <c>IMPORT /upstream/&lt;phase&gt;</c>.
/// </summary>
public static class ImportActor
{
    public const string UserName = "upstream-import";

    public const string Nickname = "Upstream import";

    /// <summary>
    /// The import account's id: the existing <c>upstream-import</c> account if there is one (it
    /// may predate this tool's id scheme or have been made by hand), else a new one (not saved in
    /// a dry run).
    /// </summary>
    public static async Task<Guid> EnsureUserAsync(AppDbContext db, bool dryRun, CancellationToken ct)
    {
        var existing = await FindByUserNameAsync(db, UserName, ct);
        if (existing is { } id)
        {
            return id;
        }

        var user = Placeholder(DeterministicIds.ImportUser, UserName, Nickname);
        if (!dryRun)
        {
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }

        return user.Id;
    }

    public static Task<Guid?> FindByUserNameAsync(AppDbContext db, string userName, CancellationToken ct)
    {
        var normalized = userName.ToUpperInvariant();
        return db.Users.AsNoTracking()
            .Where(x => x.NormalizedUserName == normalized)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public static IDisposable Begin(string phase, Guid userId)
    {
        var accessor = new HttpContextAccessor();
        var previous = accessor.HttpContext;
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, UserName),
            ],
            authenticationType: "SaBai.Import"
        );
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.Request.Method = "IMPORT";
        context.Request.Path = $"/upstream/{phase}";
        accessor.HttpContext = context;
        return new Restore(accessor, previous);
    }

    /// <summary>
    /// A staff account standing in for an upstream <c>users</c> row (upstream has no query that
    /// returns users). No password, no roles and permanently locked out, so it cannot sign in;
    /// an admin can later rename it or move its records.
    /// </summary>
    public static StaffUser Placeholder(Guid id, string userName, string nickname) =>
        new()
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Nickname = nickname,
            CreatedAt = DateTimeOffset.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.MaxValue,
        };

    private sealed class Restore(HttpContextAccessor accessor, HttpContext? previous) : IDisposable
    {
        public void Dispose() => accessor.HttpContext = previous;
    }
}
