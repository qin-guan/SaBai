using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Get;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/orders/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Get a medication order");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
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
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Order was not found.");

        await Send.OkAsync(order, ct);
    }
}
