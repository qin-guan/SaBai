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
        Roles(StaffRoles.DoctorWork);
        Summary(s =>
            s.Summary = "Place a medication order for an active ingredient and reserve its stock (earliest expiry first)"
        );
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await db.Consults.FirstOrNotFoundAsync(
            x => x.Id == req.ConsultId,
            "Consult was not found.",
            ct
        );

        var existing = await db.Orders.FindExistingAsync(
            req.Id,
            x => x.ConsultId == req.ConsultId,
            ct
        );
        var orderId = existing?.Id;
        if (orderId is null)
        {
            await db.MedicationActiveIngredients.FirstOrNotFoundAsync(
                x => x.Id == req.MedicationActiveIngredientId,
                "Medication ingredient was not found.",
                ct
            );
            orderId = await OrderLifecycle.RunAsync(
                db,
                async token =>
                {
                    var order = new SaBai.API.Domain.Order
                    {
                        Id = req.Id ?? Guid.CreateVersion7(),
                        ConsultId = req.ConsultId,
                        MedicationActiveIngredientId = req.MedicationActiveIngredientId,
                        Quantity = req.Quantity,
                        DosageInstructions = req.DosageInstructions.Trim(),
                        Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim(),
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                    await OrderLifecycle.PlaceAsync(db, order, token);
                    return order.Id;
                },
                ct
            );
        }

        var details = await db
            .Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(OrderDetails.Projection)
            .FirstAsync(ct);
        await Send.CreatedAtAsync<Get.Endpoint>(
            new { Id = orderId },
            Response.From(details),
            cancellation: ct
        );
    }
}
