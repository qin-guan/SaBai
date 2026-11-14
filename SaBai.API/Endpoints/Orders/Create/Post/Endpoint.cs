using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Orders.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/orders");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Create a pending medication order");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db.Consults.FirstOrNotFoundAsync(
            x => x.Id == req.ConsultId,
            "Consult was not found.",
            ct
        );
        var brand = await db.MedicationBrands.FirstOrNotFoundAsync(
            x => x.Id == req.MedicationBrandId,
            "Medication brand was not found.",
            ct
        );

        var order = await db
            .Orders.Include(x => x.MedicationBrand)
            .FindExistingAsync(req.Id, x => x.ConsultId == req.ConsultId, ct);
        if (order is not null)
        {
            // A resent create: answer with the order as it is now (it may have been approved).
            brand = order.MedicationBrand;
        }
        else
        {
            order = new SaBai.API.Domain.Order
            {
                Id = req.Id ?? Guid.CreateVersion7(),
                ConsultId = req.ConsultId,
                MedicationBrandId = brand.Id,
                Quantity = req.Quantity,
                DosageInstructions = req.DosageInstructions.Trim(),
                Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim(),
                Status = OrderStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { order.Id },
            new Response(
                order.Id,
                order.ConsultId,
                order.MedicationBrandId,
                brand.Name,
                order.Quantity,
                order.Status,
                order.DosageInstructions,
                order.Remarks,
                order.ApprovedById,
                order.ApprovedAt,
                order.CreatedAt
            ),
            cancellation: ct
        );
    }
}
