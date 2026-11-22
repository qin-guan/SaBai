using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using OrderResponse = SaBai.API.Endpoints.Orders.List.Get.Response;

namespace SaBai.API.Endpoints.Orders.List.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Get("/orders");
        Roles(StaffRoles.OrderWork);
        Summary(s => s.Summary = "List medication orders");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            if (!TryParseStatus(req.Status, out var status))
            {
                throw new BadRequestException("status must be PENDING, DISPENSED, or CANCELLED.");
            }

            query = query.Where(x => x.Status == status);
        }

        var orders = await query
            .OrderByDescending(x => x.Id)
            .Select(OrderDetails.Projection)
            .ToListAsync(ct);

        await Send.OkAsync([.. orders.Select(OrderResponse.From)], ct);
    }

    private static bool TryParseStatus(string value, out OrderStatus status)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "PENDING":
                status = OrderStatus.Pending;
                return true;
            case "DISPENSED":
                status = OrderStatus.Dispensed;
                return true;
            case "CANCELLED":
                status = OrderStatus.Cancelled;
                return true;
            default:
                status = default;
                return false;
        }
    }
}
