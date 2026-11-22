using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/orders/{Id}");
        Roles(StaffRoles.DoctorWork);
        Summary(s => s.Summary = "Delete a medication order (a pending order gives its reserved stock back)");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userId = User.Id();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var order = await db
                .Orders.Include(x => x.Items)
                .ThenInclude(x => x.Stock)
                .FirstOrNotFoundAsync(x => x.Id == req.Id, "Order was not found.", ct);

            // A pending order still holds stock; give it back before the order goes away.
            if (order.Status == OrderStatus.Pending)
            {
                OrderStock.Release(db, order, userId, DateTimeOffset.UtcNow);
            }

            db.Orders.Remove(order);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        await Send.NoContentAsync(ct);
    }
}
