using P = SaBai.Import.Upstream.UpstreamClient.Procedures;

namespace SaBai.Import.Upstream;

/// <summary>Everything the data phase reads from upstream, fetched in dependency order.</summary>
public sealed class UpstreamSnapshot
{
    public required IReadOnlyList<UpVillageCode> VillageCodes { get; init; }
    public required IReadOnlyList<UpPatient> Patients { get; init; }
    public required IReadOnlyList<UpVisit> Visits { get; init; }
    public required IReadOnlyList<UpVitals> Vitals { get; init; }
    public required IReadOnlyList<UpPuberty> Puberty { get; init; }
    public required IReadOnlyList<UpEyesight> Eyesight { get; init; }
    public required IReadOnlyList<UpConsult> Consults { get; init; }
    public required IReadOnlyList<UpReferral> Referrals { get; init; }
    public required IReadOnlyList<UpReferralPartner> ReferralPartners { get; init; }
    public required IReadOnlyList<UpIngredient> Ingredients { get; init; }
    public required IReadOnlyList<UpBrand> Brands { get; init; }
    public required IReadOnlyList<UpStock> Stock { get; init; }
    public required IReadOnlyList<UpBroadcast> Broadcasts { get; init; }

    /// <summary>
    /// Reads all upstream data. Upstream's list queries return every row (no pagination), except
    /// <c>broadcastsRouter.list</c>, which caps at 200 unless <c>since</c> is given: with
    /// <c>since: 0</c> it returns every broadcast. Vitals, eyesight and consults have no list
    /// query, so they are read per visit; puberty per vitals record.
    /// </summary>
    public static async Task<UpstreamSnapshot> FetchAsync(UpstreamClient upstream, Action<string> log, CancellationToken ct)
    {
        async Task<IReadOnlyList<T>> List<T>(string procedure, object? input = null)
        {
            var rows = await upstream.QueryAsync<List<T>>(procedure, input, ct);
            log($"  {procedure}: {rows.Count}");
            return rows;
        }

        log("Fetching upstream data (read-only queries)...");
        var villages = await List<UpVillageCode>(P.VillageCodesList, new { includeHidden = true });
        var patients = await List<UpPatient>(P.PatientsList);
        var visits = await List<UpVisit>(P.VisitsList);
        var partners = await List<UpReferralPartner>(P.ReferralPartnersList);
        var ingredients = await List<UpIngredient>(P.IngredientsList);
        var brands = await List<UpBrand>(P.BrandsList);
        var stock = await List<UpStock>(P.StockList);
        var referrals = await List<UpReferral>(P.ReferralsList);
        var broadcasts = await List<UpBroadcast>(P.BroadcastsList, new { since = 0 });

        // Per-visit records. The client's semaphore bounds how many run at once.
        var perVisit = await Task.WhenAll(
            visits.Select(async visit =>
            {
                var vitals = await upstream.QueryAsync<UpVitals?>(P.VitalsByVisit, new { visitId = visit.Id }, ct);
                var eyesight = await upstream.QueryAsync<UpEyesight?>(P.EyesightByVisit, new { visitId = visit.Id }, ct);
                var consults = await upstream.QueryAsync<List<UpConsult>>(P.ConsultsByVisit, new { visitId = visit.Id }, ct);
                var puberty = vitals is null
                    ? null
                    : await upstream.QueryAsync<UpPuberty?>(P.PubertyByVitals, new { vitalId = vitals.Id }, ct);
                return (vitals, eyesight, consults, puberty);
            })
        );
        log($"  per-visit queries for {visits.Count} visits done");

        return new UpstreamSnapshot
        {
            VillageCodes = villages,
            Patients = patients,
            Visits = visits,
            Vitals = [.. perVisit.Select(x => x.vitals).OfType<UpVitals>()],
            Eyesight = [.. perVisit.Select(x => x.eyesight).OfType<UpEyesight>()],
            Consults = [.. perVisit.SelectMany(x => x.consults)],
            Puberty = [.. perVisit.Select(x => x.puberty).OfType<UpPuberty>()],
            Referrals = referrals,
            ReferralPartners = partners,
            Ingredients = ingredients,
            Brands = brands,
            Stock = stock,
            Broadcasts = broadcasts,
        };
    }
}
