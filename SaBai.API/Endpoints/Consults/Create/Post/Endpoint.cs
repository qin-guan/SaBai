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
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Record a consult with diagnoses and optional prescriptions");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
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

        await EnsureOrdersAsync(req, ct);

        var now = DateTimeOffset.UtcNow;
        var consult = new Consult
        {
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
                new Diagnosis { Details = details, Category = diagnosis.Category }
            );
        }

        foreach (var order in req.Orders ?? [])
        {
            consult.Orders.Add(
                new SaBai.API.Domain.Order
                {
                    MedicationBrandId = order.MedicationBrandId,
                    DosageInstructions = order.DosageInstructions ?? "",
                    Remarks = order.Remarks,
                    Status = OrderStatus.Pending,
                    Quantity = order.Quantity,
                    CreatedAt = now,
                }
            );
        }

        db.Consults.Add(consult);
        await db.SaveChangesAsync(ct);
        return consult;
    }

    private async Task EnsureOrdersAsync(Request req, CancellationToken ct)
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

        var brandIds = orders.Select(x => x.MedicationBrandId).Distinct().ToArray();
        var found = await db.MedicationBrands.CountAsync(x => brandIds.Contains(x.Id), ct);
        if (found != brandIds.Length)
        {
            throw new BadRequestException("Medication brand was not found.");
        }
    }
}
