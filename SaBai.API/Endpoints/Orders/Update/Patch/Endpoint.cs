using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/orders/{Id}");
        Roles(StaffRoles.OrderWork);
        Summary(s =>
            s.Summary = "Change a pending order (dosage, remarks, quantity, ingredient), dispense it or cancel it"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userId = User.Id();
        await OrderLifecycle.RunAsync(
            db,
            async token =>
            {
                var order = await OrderLifecycle.LoadAsync(db, req.Id, token);
                await OrderLifecycle.ChangeAsync(
                    db,
                    order,
                    new OrderChange(
                        req.DosageInstructions,
                        req.Remarks,
                        req.Quantity,
                        req.MedicationActiveIngredientId
                    ),
                    token
                );
                switch (req.Status)
                {
                    case OrderStatus.Dispensed:
                        OrderLifecycle.Dispense(db, order, userId, DateTimeOffset.UtcNow);
                        break;
                    case OrderStatus.Cancelled:
                        OrderLifecycle.Cancel(db, order);
                        break;
                }
            },
            ct
        );

        var details = await db
            .Orders.AsNoTracking()
            .Where(x => x.Id == req.Id)
            .Select(OrderDetails.Projection)
            .FirstAsync(ct);
        await Send.OkAsync(Response.From(details), ct);
    }
}
