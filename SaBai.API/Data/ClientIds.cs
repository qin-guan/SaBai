using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Domain;

namespace SaBai.API.Data;

/// <summary>
/// Create requests may carry the id the client chose for the new record, so a device can
/// refer to a record before the server has it and can safely resend a create it is unsure
/// was received. A resent create finds its record already there and answers with it instead
/// of inserting a second one.
/// </summary>
public static class ClientIds
{
    /// <summary>
    /// Looks up the record a create request names by its client-chosen id. Returns null when
    /// the request has no id or no record has it yet (insert it); returns the record when it
    /// exists and <paramref name="belongs"/> holds (a resent create: answer with it, change
    /// nothing). Throws <see cref="ConflictException"/> when the id is taken by a record that
    /// does not belong where the request puts it.
    /// </summary>
    public static async Task<T?> FindExistingAsync<T>(
        this IQueryable<T> rows,
        Guid? id,
        Func<T, bool> belongs,
        CancellationToken ct
    )
        where T : class
    {
        if (id is not { } value)
        {
            return null;
        }

        var existing = await rows.AsNoTracking()
            .FirstOrDefaultAsync(x => EF.Property<Guid>(x, "Id") == value, ct);
        if (existing is null)
        {
            return null;
        }

        return belongs(existing)
            ? existing
            : throw new ConflictException($"Id {value} is already used by another record.");
    }

    /// <inheritdoc cref="FindExistingAsync{T}(IQueryable{T}, Guid?, Func{T, bool}, CancellationToken)"/>
    public static Task<T?> FindExistingAsync<T>(this IQueryable<T> rows, Guid? id, CancellationToken ct)
        where T : class => rows.FindExistingAsync(id, _ => true, ct);

    /// <summary>A client-chosen id is optional, but when sent it must be a real id.</summary>
    public static IRuleBuilderOptions<T, Guid?> ClientId<T>(this IRuleBuilder<T, Guid?> rule) =>
        rule.NotEqual(Guid.Empty).WithMessage("'{PropertyName}' must not be the empty id.");
}
