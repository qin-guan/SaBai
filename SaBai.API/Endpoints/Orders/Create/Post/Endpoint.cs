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
            s.Summary = "Place a medication order and reserve its stock (earliest expiry first)"
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
            orderId = await PlaceAsync(req, User.Id(), ct);
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

    private async Task<Guid> PlaceAsync(Request req, Guid userId, CancellationToken ct)
    {
        var (ingredientId, brandId) = await ResolveAsync(req, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var order = new SaBai.API.Domain.Order
            {
                Id = req.Id ?? Guid.CreateVersion7(),
                ConsultId = req.ConsultId,
                MedicationActiveIngredientId = ingredientId,
                MedicationBrandId = brandId,
                Quantity = req.Quantity,
                DosageInstructions = req.DosageInstructions.Trim(),
                Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim(),
                Status = OrderStatus.Pending,
                CreatedAt = now,
            };
            db.Orders.Add(order);
            await OrderStock.ReserveAsync(db, order, userId, now, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return order.Id;
        });
    }

    private async Task<(Guid IngredientId, Guid? BrandId)> ResolveAsync(
        Request req,
        CancellationToken ct
    )
    {
        if (req.MedicationBrandId is { } brandId && brandId != Guid.Empty)
        {
            var brand = await db
                .MedicationBrands.AsNoTracking()
                .FirstOrNotFoundAsync(x => x.Id == brandId, "Medication brand was not found.", ct);
            if (
                req.MedicationActiveIngredientId is { } given
                && given != Guid.Empty
                && given != brand.ActiveIngredientId
            )
            {
                throw new BadRequestException(
                    "The medication brand does not belong to that active ingredient."
                );
            }

            return (brand.ActiveIngredientId, brand.Id);
        }

        var ingredientId = req.MedicationActiveIngredientId!.Value;
        await db.MedicationActiveIngredients.FirstOrNotFoundAsync(
            x => x.Id == ingredientId,
            "Medication ingredient was not found.",
            ct
        );
        return (ingredientId, null);
    }
}
