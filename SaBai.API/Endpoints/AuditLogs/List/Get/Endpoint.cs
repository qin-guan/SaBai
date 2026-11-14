using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.AuditLogs.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public const int DefaultTake = 50;
    public const int MaxTake = 200;

    public override void Configure()
    {
        Get("/audit-logs");
        Roles(StaffRoles.Admin);
        Summary(s =>
        {
            s.Summary = "List audit log entries";
            s.Description =
                "Who created, changed or deleted which record, and staff sign-ins, newest first. "
                + "Admin only, because entries include the old and new values of patient records. "
                + "Page with `before` set to the previous page's `nextBefore`.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(req.EntityType))
        {
            var type = req.EntityType.Trim();
            query = query.Where(x => x.EntityType == type);
        }

        if (!string.IsNullOrWhiteSpace(req.EntityId))
        {
            var id = req.EntityId.Trim();
            query = query.Where(x => x.EntityId == id);
        }

        if (req.UserId is { } userId)
        {
            query = query.Where(x => x.UserId == userId);
        }

        if (req.Action is { } action)
        {
            query = query.Where(x => x.Action == action);
        }

        if (req.From is { } from)
        {
            query = query.Where(x => x.OccurredAt >= from);
        }

        if (req.To is { } to)
        {
            query = query.Where(x => x.OccurredAt < to);
        }

        if (req.Before is { } before)
        {
            query = query.Where(x => x.Id.CompareTo(before) < 0);
        }

        var take = req.Take ?? DefaultTake;
        // Ids increase with time, so they order the log and make a stable cursor.
        var rows = await query.OrderByDescending(x => x.Id).Take(take + 1).ToListAsync(ct);
        var hasMore = rows.Count > take;
        var page = rows.Take(take)
            .Select(x => new AuditLogEntryResponse(
                x.Id,
                x.OccurredAt,
                x.Action,
                x.UserId,
                x.UserName,
                x.EntityType,
                x.EntityId,
                x.Changes.Select(c => new AuditFieldChangeResponse(c.Field, c.Old, c.New)).ToList(),
                x.Details,
                x.IpAddress,
                x.Request
            ))
            .ToList();
        await Send.OkAsync(new Response(page, hasMore ? page[^1].Id : (Guid?)null), ct);
    }
}
