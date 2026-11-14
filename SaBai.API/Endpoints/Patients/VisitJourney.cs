using Microsoft.EntityFrameworkCore;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Patients;

/// <summary>
/// Where a visit is in the clinic flow: vitals → vision → consult → dental
/// (optional) → pharmacy (optional). Lets staff see a patient's journey at a
/// glance without loading each station's record.
/// </summary>
/// <param name="Vitals">Vitals have been recorded.</param>
/// <param name="Vision">An eyesight record exists.</param>
/// <param name="Consult">At least one consult has been recorded.</param>
/// <param name="Dental">A dental consult has been recorded.</param>
/// <param name="DentalReferred">A consult referred the patient to dental.</param>
/// <param name="PharmacyOrders">Medication orders from this visit's consults, excluding cancelled ones.</param>
/// <param name="PharmacyPending">Orders still waiting to be dispensed.</param>
/// <param name="VitalsAt">When vitals were first recorded (null if unknown).</param>
/// <param name="VisionAt">When the eyesight record was first saved (null if unknown).</param>
/// <param name="ConsultAt">When the first consult was recorded.</param>
/// <param name="DentalAt">When the dental consult was first saved (null if unknown).</param>
/// <param name="PharmacyAt">When the last order was dispensed.</param>
/// <remarks>The timestamps let the client measure each station's throughput and estimate waits.</remarks>
public sealed record VisitJourney(
    bool Vitals,
    bool Vision,
    bool Consult,
    bool Dental,
    bool DentalReferred,
    int PharmacyOrders,
    int PharmacyPending,
    DateTimeOffset? VitalsAt,
    DateTimeOffset? VisionAt,
    DateTimeOffset? ConsultAt,
    DateTimeOffset? DentalAt,
    DateTimeOffset? PharmacyAt
)
{
    public static readonly VisitJourney Empty = new(
        false,
        false,
        false,
        false,
        false,
        0,
        0,
        null,
        null,
        null,
        null,
        null
    );

    /// <summary>Loads the journey of each visit in <paramref name="visitIds"/>, in one query.</summary>
    public static async Task<Dictionary<Guid, VisitJourney>> ForVisitsAsync(
        IQueryable<Visit> visits,
        IReadOnlyCollection<Guid> visitIds,
        CancellationToken ct
    )
    {
        if (visitIds.Count == 0)
        {
            return [];
        }

        var rows = await visits
            .AsNoTracking()
            .Where(x => visitIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                Journey = new VisitJourney(
                    x.Vitals != null,
                    x.Eyesight != null,
                    x.Consults.Any(),
                    x.DentalConsult != null,
                    x.Consults.Any(c =>
                        (c.ReferredFor != null && c.ReferredFor.ToLower().Contains("dental"))
                        || c.Referrals.Any(r => r.ReferredFor.ToLower().Contains("dental"))
                    ),
                    x.Consults.SelectMany(c => c.Orders)
                        .Count(o => o.Status != OrderStatus.Cancelled),
                    x.Consults.SelectMany(c => c.Orders).Count(o => o.Status == OrderStatus.Pending),
                    x.Vitals != null ? x.Vitals.RecordedAt : null,
                    x.Eyesight != null ? x.Eyesight.RecordedAt : null,
                    x.Consults.Min(c => (DateTimeOffset?)c.Date),
                    x.DentalConsult != null ? x.DentalConsult.RecordedAt : null,
                    x.Consults.SelectMany(c => c.Orders).Max(o => o.ApprovedAt)
                ),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.Id, x => x.Journey);
    }
}
