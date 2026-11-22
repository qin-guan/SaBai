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

        await CheckOrdersAsync(req, ct);

        var now = DateTimeOffset.UtcNow;

        // The consult and its orders succeed or fail together: if any order can't be covered by
        // the available stock, nothing is saved. Built inside the unit, which may run again.
        return await OrderLifecycle.RunAsync(
            db,
            async token =>
            {
                var consult = BuildConsult(req, doctorId, now);
                db.Consults.Add(consult);
                foreach (var input in req.Orders ?? [])
                {
                    var order = new SaBai.API.Domain.Order
                    {
                        Id = input.Id ?? Guid.CreateVersion7(),
                        ConsultId = consult.Id,
                        Consult = consult,
                        MedicationActiveIngredientId = input.MedicationActiveIngredientId,
                        DosageInstructions = input.DosageInstructions?.Trim() ?? "",
                        Remarks = string.IsNullOrWhiteSpace(input.Remarks) ? null : input.Remarks.Trim(),
                        Quantity = input.Quantity,
                        CreatedAt = now,
                    };
                    await OrderLifecycle.PlaceAsync(db, order, token);
                }

                return consult;
            },
            ct
        );
    }

    private static Consult BuildConsult(Request req, Guid doctorId, DateTimeOffset now)
    {
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

        return consult;
    }

    private async Task CheckOrdersAsync(Request req, CancellationToken ct)
    {
        var orders = req.Orders ?? [];
        if (orders.Count == 0)
        {
            return;
        }

        if (orders.Any(x => x.Quantity < 1))
        {
            throw new BadRequestException("Order quantity must be at least 1.");
        }

        var ingredientIds = orders.Select(x => x.MedicationActiveIngredientId).Distinct().ToArray();
        var knownIngredients = await db
            .MedicationActiveIngredients.AsNoTracking()
            .CountAsync(x => ingredientIds.Contains(x.Id), ct);
        if (knownIngredients != ingredientIds.Length)
        {
            throw new BadRequestException("Medication ingredient was not found.");
        }
    }
}
