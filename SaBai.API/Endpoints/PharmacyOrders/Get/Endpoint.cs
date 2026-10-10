using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.PharmacyOrders.Get;

public sealed class Endpoint(AppDbContext db) : EndpointWithoutRequest<List<Response>>
{
    public override void Configure()
    {
        Get("/pharmacy-orders");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "List pending pharmacy orders grouped by patient and visit");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pending = await db.Orders
            .AsNoTracking()
            .Where(x => x.Status == OrderStatus.Pending)
            .Select(x => new
            {
                x.Id,
                x.Quantity,
                x.DosageInstructions,
                MedicationName = x.MedicationBrand.Name,
                IngredientId = x.MedicationBrand.ActiveIngredientId,
                FallBelow = x.MedicationBrand.ActiveIngredient.FallBelow,
                VisitId = (int?)x.Consult.Visit.Id,
                VisitDate = (DateTimeOffset?)x.Consult.Visit.Date,
                PatientId = (int?)x.Consult.Visit.Patient.Id,
                PatientName = x.Consult.Visit.Patient.Name,
                VillageCode = x.Consult.Visit.Patient.VillageCode != null
                    ? x.Consult.Visit.Patient.VillageCode.Code
                    : null,
            })
            .ToListAsync(ct);

        var usable = pending
            .Where(x => x.VisitId is not null && x.PatientId is not null && !string.IsNullOrWhiteSpace(x.PatientName))
            .ToList();

        var ingredientIds = usable.Select(x => x.IngredientId).Distinct().ToList();
        var activeByIngredient = new Dictionary<int, int>();
        if (ingredientIds.Count > 0)
        {
            var activeRows = await db.MedicationStocks
                .AsNoTracking()
                .Where(x => x.StockStatus == StockStatus.Active && ingredientIds.Contains(x.MedicationBrand.ActiveIngredientId))
                .Select(x => new { x.MedicationBrand.ActiveIngredientId, x.Quantity })
                .ToListAsync(ct);
            activeByIngredient = activeRows
                .GroupBy(x => x.ActiveIngredientId)
                .ToDictionary(x => x.Key, x => x.Sum(row => row.Quantity));
        }

        var visitIds = usable.Select(x => x.VisitId!.Value).Distinct().ToList();
        var diagnoses = new List<(int VisitId, DiagnosisCategory? Category, string Details)>();
        if (visitIds.Count > 0)
        {
            var loaded = await db.Diagnoses
                .AsNoTracking()
                .Where(x => visitIds.Contains(x.Consult.VisitId))
                .Select(x => new { x.Consult.VisitId, x.Category, x.Details })
                .ToListAsync(ct);
            diagnoses = loaded.Select(x => (x.VisitId, x.Category, x.Details)).ToList();
        }

        var grouped = usable
            .GroupBy(x => x.PatientId!.Value)
            .OrderBy(x => x.Key)
            .Select(patientGroup =>
            {
                var header = patientGroup.First();
                var visits = patientGroup
                    .GroupBy(x => x.VisitId!.Value)
                    .OrderBy(x => x.Min(row => row.VisitDate))
                    .ThenBy(x => x.Key)
                    .Select(visitGroup =>
                    {
                        var visit = visitGroup.First();
                        var orders = visitGroup
                            .OrderBy(x => x.Id)
                            .Select(x => new PharmacyOrderItem(
                                x.Id,
                                x.MedicationName,
                                x.Quantity,
                                x.FallBelow > activeByIngredient.GetValueOrDefault(x.IngredientId),
                                x.DosageInstructions))
                            .ToList();
                        var visitDiagnoses = diagnoses
                            .Where(x => x.VisitId == visit.VisitId)
                            .Select(x => new DiagnosisItem(x.Category, x.Details))
                            .ToList();
                        return new VisitBundle(visit.VisitId!.Value, visit.VisitDate!.Value, orders, visitDiagnoses);
                    })
                    .ToList();

                return new Response(
                    new PatientHeader(
                        Patient.FormatDisplayId(header.VillageCode, header.PatientId!.Value),
                        header.PatientName!,
                        header.VillageCode),
                    visits);
            })
            .ToList();

        await Send.OkAsync(grouped, ct);
    }
}
