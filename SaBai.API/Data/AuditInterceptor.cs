using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SaBai.API.Domain;

namespace SaBai.API.Data;

/// <summary>
/// Writes an <see cref="AuditLogEntry"/> for every row created, updated or deleted
/// through <see cref="AppDbContext"/>, attributed to the signed-in staff user of the
/// current request.
/// </summary>
/// <remarks>
/// Updates and deletes are logged in the same <c>SaveChanges</c> (so in the same
/// transaction) as the change. Rows whose key the database generates only get their
/// id after the insert, so their entries are written by a follow-up <c>SaveChanges</c>
/// straight after; inside an explicit transaction that is still the same transaction.
/// Bulk <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> bypass the change tracker and are
/// not logged, so endpoints should load and remove rows instead.
/// </remarks>
public sealed class AuditInterceptor(IHttpContextAccessor http, TimeProvider clock)
    : SaveChangesInterceptor
{
    /// <summary>Longest value kept per column; longer text is cut with an ellipsis.</summary>
    internal const int MaxValueLength = 2000;

    internal const string Redacted = "[redacted]";

    private static readonly HashSet<Type> IgnoredTypes =
    [
        typeof(AuditLogEntry),
        typeof(AuditFieldChange),
        typeof(DataProtectionKey),
    ];

    /// <summary>Columns that change on every Identity write and say nothing to a reader.</summary>
    private static readonly HashSet<string> IgnoredProperties = ["ConcurrencyStamp"];

    /// <summary>Columns whose change is worth knowing about but whose value is a secret.</summary>
    private static readonly HashSet<string> SecretProperties =
    [
        nameof(StaffUser.PasswordHash),
        nameof(StaffUser.SecurityStamp),
    ];

    private readonly ConditionalWeakTable<DbContext, List<Pending>> _pending = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (TakePending(eventData.Context) is { } context)
        {
            context.SaveChanges();
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (TakePending(eventData.Context) is { } context)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) =>
        Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // EF detects changes after this interceptor runs; do it now to see them.
        context.ChangeTracker.DetectChanges();
        var entries = context
            .ChangeTracker.Entries()
            .Where(x =>
                x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && !IgnoredTypes.Contains(x.Metadata.ClrType)
                && !x.Metadata.IsOwned()
            )
            .ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var actor = AuditActor.From(http.HttpContext);
        var now = clock.GetUtcNow();
        List<Pending>? pending = null;
        foreach (var entry in entries)
        {
            var log = Describe(entry, actor, now);
            if (log is null)
            {
                continue;
            }

            if (entry.State == EntityState.Added && HasTemporaryKey(entry))
            {
                (pending ??= []).Add(new Pending(entry, log));
            }
            else
            {
                log.EntityId = KeyOf(entry);
                context.Add(log);
            }
        }

        if (pending is not null)
        {
            _pending.AddOrUpdate(context, pending);
        }
    }

    /// <summary>Hands back the context when inserted rows still need their entries written.</summary>
    private DbContext? TakePending(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
        {
            return null;
        }

        _pending.Remove(context);
        foreach (var (entry, log) in pending)
        {
            log.EntityId = KeyOf(entry);
            context.Add(log);
        }

        return context;
    }

    private void Discard(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }

    private static AuditLogEntry? Describe(EntityEntry entry, AuditActor actor, DateTimeOffset now)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Deleted => AuditAction.Deleted,
            _ => AuditAction.Updated,
        };
        var redactAll = IsTokenStore(entry.Metadata.ClrType);
        var changes = new List<AuditFieldChange>();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (IgnoredProperties.Contains(name)
                || (property.Metadata.IsShadowProperty() && !property.Metadata.IsForeignKey()))
            {
                continue;
            }

            var secret = SecretProperties.Contains(name) || (redactAll && !property.Metadata.IsKey());
            switch (action)
            {
                case AuditAction.Created when property.CurrentValue is not null:
                    // Keys are in EntityId; join rows (e.g. user roles) keep theirs to show the link.
                    if (!property.Metadata.IsPrimaryKey() || property.Metadata.IsForeignKey())
                    {
                        changes.Add(Change(name, null, property.CurrentValue, secret));
                    }

                    break;
                case AuditAction.Deleted when property.OriginalValue is not null:
                    if (!property.Metadata.IsPrimaryKey() || property.Metadata.IsForeignKey())
                    {
                        changes.Add(Change(name, property.OriginalValue, null, secret));
                    }

                    break;
                case AuditAction.Updated
                    when property.IsModified
                        && !Equals(property.OriginalValue, property.CurrentValue):
                    changes.Add(Change(name, property.OriginalValue, property.CurrentValue, secret));
                    break;
            }
        }

        // Nothing a reader would see changed (e.g. only the concurrency stamp).
        if (action == AuditAction.Updated && changes.Count == 0)
        {
            return null;
        }

        return new AuditLogEntry
        {
            OccurredAt = now,
            Action = action,
            UserId = actor.UserId,
            UserName = actor.UserName,
            EntityType = TypeName(entry.Metadata.ClrType),
            Changes = changes,
            IpAddress = actor.IpAddress,
            Request = actor.Request,
        };
    }

    private static AuditFieldChange Change(string field, object? old, object? next, bool secret) =>
        new()
        {
            Field = field,
            Old = secret ? (old is null ? null : Redacted) : Format(old),
            New = secret ? (next is null ? null : Redacted) : Format(next),
        };

    /// <summary>Authenticator keys and recovery codes live in the user token table.</summary>
    private static bool IsTokenStore(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IdentityUserToken<>);

    private static bool HasTemporaryKey(EntityEntry entry) =>
        entry.Properties.Any(x => x.Metadata.IsPrimaryKey() && x.IsTemporary);

    private static string KeyOf(EntityEntry entry) =>
        string.Join(
            ",",
            entry
                .Metadata.FindPrimaryKey()!
                .Properties.Select(p =>
                    Format(entry.Property(p.Name).CurrentValue ?? entry.Property(p.Name).OriginalValue)
                )
        );

    /// <summary><c>IdentityUserRole`1</c> → <c>UserRole</c>, <c>Patient</c> → <c>Patient</c>.</summary>
    internal static string TypeName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`');
        name = tick < 0 ? name : name[..tick];
        return type.Namespace?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) == true
            && name.StartsWith("Identity", StringComparison.Ordinal)
            ? name["Identity".Length..]
            : name;
    }

    internal static string? Format(object? value)
    {
        var text = value switch
        {
            null => null,
            string s => s,
            DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
            DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            Enum e => e.ToString(),
            byte[] bytes => $"[{bytes.Length} bytes]",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable list => JsonSerializer.Serialize(list),
            _ => value.ToString(),
        };
        return text is { Length: > MaxValueLength } ? text[..(MaxValueLength - 1)] + "…" : text;
    }

    private sealed record Pending(EntityEntry Entry, AuditLogEntry Log);
}

/// <summary>Who is acting in the current request, for audit entries.</summary>
public sealed record AuditActor(Guid? UserId, string? UserName, string? IpAddress, string? Request)
{
    public static AuditActor From(HttpContext? context)
    {
        if (context is null)
        {
            return new AuditActor(null, null, null, null);
        }

        var principal = context.User;
        Guid? userId =
            principal.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : null;
        var userName = userId is null ? null : principal.FindFirstValue(ClaimTypes.Name);
        var request = $"{context.Request.Method} {context.Request.Path}";
        return new AuditActor(
            userId,
            userName,
            context.Connection.RemoteIpAddress?.ToString(),
            request.Length > 500 ? request[..500] : request
        );
    }
}
