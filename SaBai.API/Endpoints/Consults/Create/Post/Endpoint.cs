using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Consults.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/consults");
        Roles(StaffRoles.DoctorWork);
        Summary(s => s.Summary = "Record a consult with diagnoses and optional prescriptions");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var existing = await db
            .Consults.Include(x => x.Diagnoses)
            .Include(x => x.Doctor)
            .FindExistingAsync(req.Id, x => x.VisitId == req.VisitId, ct);
        if (existing is not null)
        {
            await Send.CreatedAtAsync<Get.Endpoint>(
                new { existing.Id },
                Response.From(existing),
                cancellation: ct
            );
            return;
        }

        var doctorId = User.Id();
        var consult = await InsertConsultAsync(req, doctorId, ct);
        await db.Entry(consult).Reference(x => x.Doctor).LoadAsync(ct);
        if (consult.Doctor is null)
        {
            throw new NotFoundException("Doctor was not found.");
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { consult.Id },
            Response.From(consult),
            cancellation: ct
        );
    }

    private async Task<Consult> InsertConsultAsync(Request req, Guid doctorId, CancellationToken ct)
    {
        _ = await db.Visits.FirstOrNotFoundAsync(
            x => x.Id == req.VisitId,
            "Visit was not found.",
            ct
        );
        if (req.Diagnoses is not { Count: > 0 })
        {
            throw new BadRequestException("At least one diagnosis is required.");
        }

        var resolved = await ResolveOrdersAsync(req, ct);

        var now = DateTimeOffset.UtcNow;
        var consult = new Consult
        {
            Id = req.Id ?? Guid.CreateVersion7(),
            Date = now,
            PastMedicalHistory = req.PastMedicalHistory,
            Consultation = req.Consultation,
            TreatmentPlan = req.TreatmentPlan,
            ReferredFor = req.ReferredFor,
            ReferralNotes = req.ReferralNotes,
            Remarks = req.Remarks,
            DoctorId = doctorId,
            VisitId = req.VisitId,
        };

        foreach (var diagnosis in req.Diagnoses)
        {
            var details = diagnosis.Details.Trim();
            if (details.Length == 0)
            {
                throw new BadRequestException("Diagnosis details are required.");
            }

            consult.Diagnoses.Add(
                new Diagnosis
                {
                    Id = diagnosis.Id ?? Guid.CreateVersion7(),
                    Details = details,
                    Category = diagnosis.Category,
                }
            );
        }

        // The consult and its orders succeed or fail together. Each order is saved before the
        // next one is allocated so it sees the stock the previous one reserved.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.Consults.Add(consult);
            await db.SaveChangesAsync(ct);

            foreach (var (input, ingredientId, brandId) in resolved)
            {
                var order = new SaBai.API.Domain.Order
                {
                    Id = input.Id ?? Guid.CreateVersion7(),
                    ConsultId = consult.Id,
                    MedicationActiveIngredientId = ingredientId,
                    MedicationBrandId = brandId,
                    DosageInstructions = input.DosageInstructions ?? "",
                    Remarks = input.Remarks,
                    Status = OrderStatus.Pending,
                    Quantity = input.Quantity,
                    CreatedAt = now,
                };
                db.Orders.Add(order);
                await OrderStock.ReserveAsync(db, order, doctorId, now, ct);
                await db.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
        });

        return consult;
    }

    private async Task<List<(OrderInput Input, Guid IngredientId, Guid? BrandId)>> ResolveOrdersAsync(
        Request req,
        CancellationToken ct
    )
    {
        var orders = req.Orders ?? [];
        var resolved = new List<(OrderInput, Guid, Guid?)>();
        if (orders.Count == 0)
        {
            return resolved;
        }

        if (orders.Any(x => x.Quantity < 1))
        {
            throw new BadRequestException("Order quantity must be at least 1.");
        }

        var brandIds = orders
            .Where(x => x.MedicationBrandId is { } b && b != Guid.Empty)
            .Select(x => x.MedicationBrandId!.Value)
            .Distinct()
            .ToArray();
        var brands = await db
            .MedicationBrands.AsNoTracking()
            .Where(x => brandIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        if (brands.Count != brandIds.Length)
        {
            throw new BadRequestException("Medication brand was not found.");
        }

        var ingredientIds = orders
            .Where(x => x.MedicationActiveIngredientId is { } i && i != Guid.Empty)
            .Select(x => x.MedicationActiveIngredientId!.Value)
            .Distinct()
            .ToArray();
        var knownIngredients = await db
            .MedicationActiveIngredients.AsNoTracking()
            .CountAsync(x => ingredientIds.Contains(x.Id), ct);
        if (knownIngredients != ingredientIds.Length)
        {
            throw new BadRequestException("Medication ingredient was not found.");
        }

        foreach (var order in orders)
        {
            if (order.MedicationBrandId is { } brandId && brandId != Guid.Empty)
            {
                var brand = brands[brandId];
                if (
                    order.MedicationActiveIngredientId is { } given
                    && given != Guid.Empty
                    && given != brand.ActiveIngredientId
                )
                {
                    throw new BadRequestException(
                        "The medication brand does not belong to that active ingredient."
                    );
                }

                resolved.Add((order, brand.ActiveIngredientId, brand.Id));
            }
            else
            {
                resolved.Add((order, order.MedicationActiveIngredientId!.Value, null));
            }
        }

        return resolved;
    }
}
