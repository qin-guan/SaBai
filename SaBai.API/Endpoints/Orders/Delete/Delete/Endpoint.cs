using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Orders.Delete.Delete;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request>
{
    public override void Configure()
    {
        Delete("/orders/{Id}");
        Roles(StaffRoles.DoctorWork);
        Summary(s =>
            s.Summary = "Delete a medication order that was not dispensed (a pending order gives its reserved stock back)"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await OrderLifecycle.RunAsync(
            db,
            async token =>
            {
                var order = await OrderLifecycle.LoadAsync(db, req.Id, token);
                OrderLifecycle.Remove(db, order);
            },
            ct
        );

        await Send.NoContentAsync(ct);
    }
}
