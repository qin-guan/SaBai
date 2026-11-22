using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using Domain = SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/orders/{Id}");
        Roles(StaffRoles.OrderWork);
        Summary(s =>
            s.Summary = "Update, approve (dispense) or cancel (release the stock of) a pending order"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var current = await db
            .Orders.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.Id, "Order was not found.", ct);
        if (current.Status is OrderStatus.Approved or OrderStatus.Cancelled)
        {
            throw new BadRequestException(
                $"Order is already {current.Status.ToString().ToUpperInvariant()}."
            );
        }

        await ChangeAsync(req, ct);

        var order = await db
            .Orders.AsNoTracking()
            .Where(x => x.Id == req.Id)
            .Select(OrderDetails.Projection)
            .FirstAsync(ct);
        await Send.OkAsync(Response.From(order), ct);
    }

    private async Task ChangeAsync(Request req, CancellationToken ct)
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
            if (order.Status is OrderStatus.Approved or OrderStatus.Cancelled)
            {
                throw new BadRequestException(
                    $"Order is already {order.Status.ToString().ToUpperInvariant()}."
                );
            }

            ApplyNotes(order, req);
            var now = DateTimeOffset.UtcNow;
            if (req.Status == OrderStatus.Approved)
            {
                await ApproveAsync(order, userId, now, ct);
            }
            else if (req.Status == OrderStatus.Cancelled)
            {
                OrderStock.Release(db, order, userId, now);
                order.Status = OrderStatus.Cancelled;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }

    private async Task ApproveAsync(
        Domain.Order order,
        Guid userId,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        if (order.Items.Count == 0)
        {
            // An order placed before stock was reserved up front: take its stock now.
            try
            {
                await OrderStock.ReserveAsync(db, order, userId, now, ct);
            }
            catch (ConflictException)
            {
                throw new BadRequestException("Medicine stock cannot be negative.");
            }
        }

        OrderStock.Dispense(db, order, userId, now);
        order.Status = OrderStatus.Approved;
        order.ApprovedById = userId;
        order.ApprovedAt = now;
    }

    private static void ApplyNotes(Domain.Order order, Request req)
    {
        if (req.DosageInstructions is not null)
        {
            order.DosageInstructions = req.DosageInstructions.Trim();
        }

        if (req.Remarks is not null)
        {
            order.Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim();
        }
    }
}
