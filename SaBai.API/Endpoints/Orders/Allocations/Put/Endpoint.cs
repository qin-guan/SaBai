using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Orders.Allocations.Put;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Put("/orders/{Id}/allocations");
        Roles(StaffRoles.PharmacyWork);
        Summary(s =>
            s.Summary = "Replace the stock batches a pending order is dispensed from with the pharmacist's choice"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var picks = req.Allocations.ToPicks();
        await OrderLifecycle.RunAsync(
            db,
            async token =>
            {
                var order = await OrderLifecycle.LoadAsync(db, req.Id, token);
                await OrderLifecycle.ReallocateAsync(db, order, picks, token);
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
