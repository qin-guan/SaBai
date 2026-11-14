using Microsoft.EntityFrameworkCore;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Push;

/// <summary>How many of a notification's devices were reached.</summary>
/// <param name="Removed">Devices dropped because their subscription no longer exists.</param>
public sealed record PushResult(int Sent, int Removed, int Failed);

/// <summary>Sends a notification to staff devices and forgets devices that are gone.</summary>
public sealed class PushNotifier(AppDbContext db, PushSender sender)
{
    /// <summary>Notifies every device of one staff user.</summary>
    public Task<PushResult> NotifyUserAsync(
        Guid staffUserId,
        PushNotification notification,
        CancellationToken ct
    ) => NotifyAsync(db.PushSubscriptions.Where(x => x.StaffUserId == staffUserId), notification, ct);

    /// <summary>Notifies every device of every staff user.</summary>
    public Task<PushResult> NotifyEveryoneAsync(PushNotification notification, CancellationToken ct) =>
        NotifyAsync(db.PushSubscriptions, notification, ct);

    private async Task<PushResult> NotifyAsync(
        IQueryable<StaffPushSubscription> query,
        PushNotification notification,
        CancellationToken ct
    )
    {
        var subscriptions = await query.ToListAsync(ct);
        var outcomes = await Task.WhenAll(
            subscriptions.Select(x => sender.SendAsync(x, notification, ct))
        );

        var gone = subscriptions.Where((_, i) => outcomes[i] == PushOutcome.Gone).ToList();
        if (gone.Count > 0)
        {
            db.PushSubscriptions.RemoveRange(gone);
            await db.SaveChangesAsync(ct);
        }

        return new PushResult(
            outcomes.Count(x => x == PushOutcome.Sent),
            gone.Count,
            outcomes.Count(x => x == PushOutcome.Failed)
        );
    }
}
