using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.AllocationOptions.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/orders/{Id}/allocation-options");
        Roles(StaffRoles.PharmacyWork);
        Summary(s =>
            s.Summary = "List the stock batches a pending order could be dispensed from, with what it may take from each"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var order =
            await db
                .Orders.AsNoTracking()
                .Where(x => x.Id == req.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Status,
                    x.MedicationActiveIngredientId,
                    IngredientName = x.MedicationActiveIngredient.Name,
                    x.MedicationActiveIngredient.UnitOfMeasurement,
                    x.Quantity,
                })
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Order was not found.");
        if (order.Status != OrderStatus.Pending)
        {
            throw new BadRequestException(
                $"Order is already {OrderLifecycle.OrderStatusName(order.Status)}."
            );
        }

        var now = DateTimeOffset.UtcNow;
        var batches = await db
            .MedicationStocks.AsNoTracking()
            .Where(x =>
                x.MedicationBrand.ActiveIngredientId == order.MedicationActiveIngredientId
                && x.StockStatus == StockStatus.Active
                && x.Quantity > 0
            )
            .Select(x => new
            {
                x.Id,
                x.MedicationBrandId,
                BrandName = x.MedicationBrand.Name,
                x.Location,
                x.Expiry,
                x.Quantity,
                x.ReservedQuantity,
                x.Remarks,
                Allocated = x.Allocations.Where(a => a.OrderId == order.Id).Sum(a => (int?)a.Quantity)
                    ?? 0,
            })
            .Where(x => x.Expiry >= now || x.Allocated > 0)
            .OrderBy(x => x.Expiry)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        await Send.OkAsync(
            new Response(
                order.Id,
                order.Status,
                order.MedicationActiveIngredientId,
                order.IngredientName,
                order.UnitOfMeasurement,
                order.Quantity,
                [
                    .. batches.Select(x =>
                    {
                        var expired = x.Expiry < now;
                        var available = x.Quantity - x.ReservedQuantity;
                        return new BatchOption(
                            x.Id,
                            x.MedicationBrandId,
                            x.BrandName,
                            x.Location,
                            x.Expiry,
                            expired,
                            x.Quantity,
                            x.ReservedQuantity,
                            available,
                            x.Allocated,
                            expired ? 0 : available + x.Allocated,
                            x.Remarks
                        );
                    }),
                ]
            ),
            ct
        );
    }
}
