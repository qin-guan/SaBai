using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Orders.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/orders/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Delete a medication order without reversing stock");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Order was not found.",
            ct
        );
        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
