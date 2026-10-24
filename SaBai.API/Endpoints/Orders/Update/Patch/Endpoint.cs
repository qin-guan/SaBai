using System.Globalization;
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
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update or approve a medication order");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var current = await db.Orders.AsNoTracking().FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Order was not found.",
            ct);
        if (current.Status is OrderStatus.Approved or OrderStatus.Cancelled)
        {
            throw new BadRequestException($"Order is already {current.Status.ToString().ToUpperInvariant()}.");
        }

        if (req.Status == OrderStatus.Approved)
        {
            await ApproveAsync(req, ct);
        }
        else
        {
            await UpdateOpenOrderAsync(req, ct);
        }

        var order = await db.Orders
            .AsNoTracking()
            .Where(x => x.Id == req.Id)
            .Select(x => new Response(
                x.Id,
                x.ConsultId,
                x.MedicationBrandId,
                x.MedicationBrand.Name,
                x.Quantity,
                x.Status,
                x.DosageInstructions,
                x.Remarks,
                x.ApprovedById,
                x.ApprovedAt,
                x.CreatedAt))
            .FirstAsync(ct);
        await Send.OkAsync(order, ct);
    }

    private async Task UpdateOpenOrderAsync(Request req, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrNotFoundAsync(x => x.Id == req.Id, "Order was not found.", ct);
        if (order.Status is OrderStatus.Approved or OrderStatus.Cancelled)
        {
            throw new BadRequestException($"Order is already {order.Status.ToString().ToUpperInvariant()}.");
        }

        ApplyNotes(order, req);
        if (req.Status == OrderStatus.Cancelled)
        {
            order.Status = OrderStatus.Cancelled;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ApproveAsync(Request req, CancellationToken ct)
    {
        var userId = User.Id();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var order = await db.Orders.FirstOrNotFoundAsync(x => x.Id == req.Id, "Order was not found.", ct);
            if (order.Status is OrderStatus.Approved or OrderStatus.Cancelled)
            {
                throw new BadRequestException($"Order is already {order.Status.ToString().ToUpperInvariant()}.");
            }

            ApplyNotes(order, req);
            var lots = await db.MedicationStocks
                .Where(x => x.MedicationBrandId == order.MedicationBrandId
                    && x.StockStatus == StockStatus.Active
                    && x.Quantity > 0)
                .OrderBy(x => x.Expiry)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            if (lots.Sum(x => x.Quantity) < order.Quantity)
            {
                throw new BadRequestException("Medicine stock cannot be negative.");
            }

            var now = DateTimeOffset.UtcNow;
            var remaining = order.Quantity;
            var changes = new List<StockChange>();
            foreach (var lot in lots)
            {
                if (remaining == 0)
                {
                    break;
                }

                var previous = lot.Quantity;
                var take = Math.Min(lot.Quantity, remaining);
                lot.Quantity -= take;
                remaining -= take;
                if (lot.Quantity == 0)
                {
                    lot.StockStatus = StockStatus.Dispensed;
                }

                var change = new StockChange
                {
                    StockId = lot.Id,
                    Field = StockChangeField.Quantity,
                    PreviousValue = previous.ToString(CultureInfo.InvariantCulture),
                    NewValue = lot.Quantity.ToString(CultureInfo.InvariantCulture),
                    UserId = userId,
                    CreatedAt = now,
                };
                changes.Add(change);
                db.StockChanges.Add(change);
            }

            await db.SaveChangesAsync(ct);
            foreach (var change in changes)
            {
                db.OrderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    StockChangeId = change.Id,
                });
            }

            order.Status = OrderStatus.Approved;
            order.ApprovedById = userId;
            order.ApprovedAt = now;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
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
