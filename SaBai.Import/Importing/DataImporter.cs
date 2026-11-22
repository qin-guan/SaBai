using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.Import.Upstream;
using Ids = SaBai.Import.Importing.DeterministicIds;

namespace SaBai.Import.Importing;

/// <summary>
/// Phase 1: upserts an <see cref="UpstreamSnapshot"/> into the SaBai database through
/// <see cref="AppDbContext"/> (tracked entities, the same save hooks as the API), parents before
/// children. Every row's id is derived from its upstream table and id, so a re-run updates
/// changed rows and skips unchanged ones. A row that fails is reported and the rest continue;
/// its children are reported as failed too. With <c>dryRun</c> everything is mapped and checked
/// against the database, but nothing is saved.
/// </summary>
public sealed class DataImporter(IServiceProvider services, ImportReport report, bool dryRun, Action<string> log)
{
    private const int ChunkSize = 200;

    /// <summary>Upstream village id → SaBai id (normally the derived id; see village stage).</summary>
    private readonly Dictionary<int, Guid> _villages = [];
    private readonly HashSet<Guid> _present = [];

    /// <summary>Upstream user id → staff account here (see <see cref="UsersAsync"/>).</summary>
    private readonly Dictionary<Guid, Guid> _users = [];
    private Guid _importUser;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public async Task RunAsync(UpstreamSnapshot up, CancellationToken ct)
    {
        await using (var scope = services.CreateAsyncScope())
        {
            _importUser = await ImportActor.EnsureUserAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), dryRun, ct);
        }

        using var actor = ImportActor.Begin("data", _importUser);
        var visitDates = up.Visits.ToDictionary(x => x.Id, x => x.Date);

        await UsersAsync(up, ct);
        await VillagesAsync(up.VillageCodes, ct);
        await PatientsAsync(up.Patients, up.Visits, ct);

        await StageAsync("visits", up.Visits, x => x.Id.ToString(), x => Ids.Visit(x.Id), async (db, x) =>
        {
            Require(Ids.Patient(x.PatientId), "patient", x.PatientId);
            if (!_villages.TryGetValue(x.VillageCodeId, out var village))
            {
                throw new MappingException($"village code {x.VillageCodeId} was not imported");
            }

            var visit = await db.Visits.FindAsync([Ids.Visit(x.Id)], ct);
            var isNew = visit is null;
            visit ??= new Visit { Id = Ids.Visit(x.Id), Status = Visit.StartedStatus };
            UpstreamMapper.Apply(x, visit, village);
            return Track(db, visit, isNew);
        }, ct);

        await StageAsync("vitals", up.Vitals, x => x.Id.ToString(), x => Ids.Vitals(x.Id), async (db, x) =>
        {
            Require(Ids.Visit(x.VisitId), "visit", x.VisitId);
            var vitals = await db.Vitals.FindAsync([Ids.Vitals(x.Id)], ct);
            var isNew = vitals is null;
            vitals ??= new Vitals { Id = Ids.Vitals(x.Id) };
            UpstreamMapper.Apply(x, vitals, visitDates[x.VisitId]);
            return Track(db, vitals, isNew);
        }, ct);

        await StageAsync("puberty", up.Puberty, x => x.Id.ToString(), x => Ids.Puberty(x.Id), async (db, x) =>
        {
            Require(Ids.Vitals(x.VitalId), "vitals", x.VitalId);
            var puberty = await db.PubertyAssessments.FindAsync([Ids.Puberty(x.Id)], ct);
            var isNew = puberty is null;
            puberty ??= new PubertyAssessment { Id = Ids.Puberty(x.Id) };
            UpstreamMapper.Apply(x, puberty);
            return Track(db, puberty, isNew);
        }, ct);

        await StageAsync("eyesight", up.Eyesight, x => x.Id.ToString(), x => Ids.Eyesight(x.Id), async (db, x) =>
        {
            Require(Ids.Visit(x.VisitId), "visit", x.VisitId);
            var eyesight = await db.Eyesights.FindAsync([Ids.Eyesight(x.Id)], ct);
            var isNew = eyesight is null;
            eyesight ??= new Eyesight { Id = Ids.Eyesight(x.Id) };
            UpstreamMapper.Apply(x, eyesight, visitDates[x.VisitId]);
            return Track(db, eyesight, isNew);
        }, ct);

        await StageAsync("consults", up.Consults, x => x.Id.ToString(), x => Ids.Consult(x.Id), async (db, x) =>
        {
            Require(Ids.Visit(x.VisitId), "visit", x.VisitId);
            var consult = await db.Consults.FindAsync([Ids.Consult(x.Id)], ct);
            var isNew = consult is null;
            consult ??= new Consult { Id = Ids.Consult(x.Id) };
            UpstreamMapper.Apply(x, consult, _users.TryGetValue(x.DoctorId, out var doctor)
                ? doctor
                : throw new MappingException($"its doctor {x.DoctorId} has no staff account"));
            return Track(db, consult, isNew);
        }, ct);

        var diagnoses = up.Consults.SelectMany(x => x.Diagnoses ?? []).ToList();
        report.Fetched("diagnoses", diagnoses.Count);
        await StageAsync("diagnoses", diagnoses, x => x.Id.ToString(), x => Ids.Diagnosis(x.Id), async (db, x) =>
        {
            Require(Ids.Consult(x.ConsultId), "consult", x.ConsultId);
            var diagnosis = await db.Diagnoses.FindAsync([Ids.Diagnosis(x.Id)], ct);
            var isNew = diagnosis is null;
            diagnosis ??= new Diagnosis { Id = Ids.Diagnosis(x.Id), Details = "" };
            UpstreamMapper.Apply(x, diagnosis);
            return Track(db, diagnosis, isNew);
        }, ct, countFetched: false);

        await StageAsync("referral partners", up.ReferralPartners, x => x.Id.ToString(), x => Ids.ReferralPartner(x.Id), async (db, x) =>
        {
            var partner = await db.ReferralPartners.FindAsync([Ids.ReferralPartner(x.Id)], ct);
            var isNew = partner is null;
            partner ??= new ReferralPartner { Id = Ids.ReferralPartner(x.Id), Name = "" };
            UpstreamMapper.Apply(x, partner);
            return Track(db, partner, isNew);
        }, ct);

        await StageAsync("referrals", up.Referrals, x => x.Id.ToString(), x => Ids.Referral(x.Id), async (db, x) =>
        {
            Require(Ids.Consult(x.ConsultId), "consult", x.ConsultId);
            var referral = await db.Referrals.FindAsync([Ids.Referral(x.Id)], ct);
            var isNew = referral is null;
            referral ??= new Referral { Id = Ids.Referral(x.Id), ReferredFor = "" };
            UpstreamMapper.Apply(x, referral);
            return Track(db, referral, isNew);
        }, ct);

        await StageAsync("ingredients", up.Ingredients, x => x.Id.ToString(), x => Ids.Ingredient(x.Id), async (db, x) =>
        {
            var ingredient = await db.MedicationActiveIngredients.FindAsync([Ids.Ingredient(x.Id)], ct);
            var isNew = ingredient is null;
            ingredient ??= new MedicationActiveIngredient { Id = Ids.Ingredient(x.Id), Name = "", UnitOfMeasurement = "" };
            UpstreamMapper.Apply(x, ingredient);
            return Track(db, ingredient, isNew);
        }, ct);

        await StageAsync("brands", up.Brands, x => x.Id.ToString(), x => Ids.Brand(x.Id), async (db, x) =>
        {
            Require(Ids.Ingredient(x.ActiveIngredientId), "ingredient", x.ActiveIngredientId);
            var brand = await db.MedicationBrands.FindAsync([Ids.Brand(x.Id)], ct);
            var isNew = brand is null;
            brand ??= new MedicationBrand { Id = Ids.Brand(x.Id), Name = "" };
            UpstreamMapper.Apply(x, brand);
            return Track(db, brand, isNew);
        }, ct);

        await StageAsync("stock", up.Stock, x => x.Id.ToString(), x => Ids.Stock(x.Id), (db, x) => StockAsync(db, x, ct), ct);

        var senders = up.Broadcasts.Where(x => x.CreatedBy is not null)
            .GroupBy(x => x.CreatedBy!.Value)
            .ToDictionary(g => g.Key, g => g.Select(b => b.SenderName).FirstOrDefault(n => n is not null));
        await StageAsync("broadcasts", up.Broadcasts, x => x.Id.ToString(), x => Ids.Broadcast(x.Id), async (db, x) =>
        {
            var broadcast = await db.Broadcasts.FindAsync([Ids.Broadcast(x.Id)], ct);
            var isNew = broadcast is null;
            broadcast ??= new Broadcast { Id = Ids.Broadcast(x.Id), Title = "", Body = "" };
            var sender = x.SenderName ?? (x.CreatedBy is { } by ? senders.GetValueOrDefault(by) ?? PlaceholderName(by) : null);
            UpstreamMapper.Apply(x, broadcast, sender);
            return Track(db, broadcast, isNew);
        }, ct);
    }

    /// <summary>
    /// Upstream has no query that returns its <c>users</c> table; consults (doctorId) and
    /// broadcasts (createdBy, senderName) only carry user ids. Each distinct upstream user gets a
    /// placeholder staff account that cannot sign in, named after the broadcast sender name when
    /// one is known. Existing accounts are left as they are (an admin may have renamed them).
    /// </summary>
    private async Task UsersAsync(UpstreamSnapshot up, CancellationToken ct)
    {
        var names = up.Broadcasts.Where(x => x.CreatedBy is not null && x.SenderName is not null)
            .GroupBy(x => x.CreatedBy!.Value)
            .ToDictionary(g => g.Key, g => g.First().SenderName!);
        var ids = up.Consults.Select(x => x.DoctorId)
            .Concat(up.Broadcasts.Where(x => x.CreatedBy is not null).Select(x => x.CreatedBy!.Value))
            .Distinct()
            .ToList();

        await StageAsync("staff users", ids, x => x.ToString(), Ids.User, async (db, x) =>
        {
            // An account with this user name may already exist (renamed by an admin, or made by an
            // earlier version of this tool): use it as it is.
            var userName = $"upstream-{x:N}";
            if (await ImportActor.FindByUserNameAsync(db, userName, ct) is { } existing)
            {
                _users[x] = existing;
                return Outcome.Skipped;
            }

            _users[x] = Ids.User(x);
            db.Users.Add(ImportActor.Placeholder(Ids.User(x), userName, names.GetValueOrDefault(x) ?? PlaceholderName(x)));
            return Outcome.Created;
        }, ct);
    }

    private static string PlaceholderName(Guid upstreamUserId) => $"Upstream user {upstreamUserId.ToString("N")[..8]}";

    /// <summary>
    /// Village codes are unique here. When a village with the same code already exists (made
    /// by hand before the import), upstream rows are linked to it and it is left unchanged.
    /// </summary>
    private async Task VillagesAsync(IReadOnlyList<UpVillageCode> rows, CancellationToken ct)
    {
        await StageAsync("village codes", rows, x => x.Id.ToString(), x => Ids.VillageCode(x.Id), async (db, x) =>
        {
            var id = Ids.VillageCode(x.Id);
            var village = await db.VillageCodes.FindAsync([id], ct);
            if (village is null)
            {
                var sameCode = await db.VillageCodes.AsNoTracking()
                    .Where(v => v.Code == x.Code)
                    .Select(v => (Guid?)v.Id)
                    .FirstOrDefaultAsync(ct);
                if (sameCode is { } existing)
                {
                    _villages[x.Id] = existing;
                    _present.Add(id);
                    report.Note($"village code {x.Code} (upstream {x.Id}) already existed; linked to it and left unchanged");
                    return Outcome.Skipped;
                }
            }

            _villages[x.Id] = id;
            var isNew = village is null;
            village ??= new VillageCode { Id = id, Code = "", Name = "", ColorHex = "" };
            UpstreamMapper.Apply(x, village);
            return Track(db, village, isNew);
        }, ct);
    }

    /// <summary>
    /// Patients keep their upstream id as <see cref="Patient.Number"/> (upstream shows
    /// <c>villageCode + id</c> on cards and screens, and SaBai shows <c>villageCode + Number</c>),
    /// unless another patient here already has that number: then Postgres assigns a new one and
    /// it is reported. Afterwards the number sequence is moved past the highest number so
    /// patients registered through the API never collide with imported ones.
    /// </summary>
    private async Task PatientsAsync(IReadOnlyList<UpPatient> rows, IReadOnlyList<UpVisit> visits, CancellationToken ct)
    {
        var byPatient = visits.GroupBy(x => x.PatientId).ToDictionary(g => g.Key, g => g.OrderBy(v => v.Date).ToList());
        var renumber = new List<UpPatient>();

        async Task<Outcome> Apply(AppDbContext db, UpPatient x, bool keepNumber)
        {
            var patientVisits = byPatient.GetValueOrDefault(x.Id) ?? [];
            Guid? village = null;
            if (patientVisits.Count > 0)
            {
                // The latest visit's village, as upstream shows it next to the patient id.
                village = _villages.TryGetValue(patientVisits[^1].VillageCodeId, out var v)
                    ? v
                    : throw new MappingException($"village code {patientVisits[^1].VillageCodeId} was not imported");
            }

            var id = Ids.Patient(x.Id);
            var patient = await db.Patients.FindAsync([id], ct);
            var isNew = patient is null;
            if (patient is null)
            {
                patient = new Patient { Id = id, Name = "", DrugAllergy = "" };
                if (keepNumber)
                {
                    if (await db.Patients.AnyAsync(p => p.Number == x.Id, ct))
                    {
                        renumber.Add(x);
                        return Outcome.Skipped; // handled in the second pass
                    }

                    patient.Number = x.Id;
                }
            }

            var createdAt = patientVisits.Count > 0 ? patientVisits[0].Date : patient.CreatedAt == default ? _now : patient.CreatedAt;
            UpstreamMapper.Apply(x, patient, village, createdAt);
            return Track(db, patient, isNew);
        }

        await StageAsync("patients", rows, x => x.Id.ToString(), x => Ids.Patient(x.Id), (db, x) => Apply(db, x, true), ct,
            skipWhen: x => renumber.Contains(x));

        if (!dryRun && rows.Count > 0)
        {
            await AdvancePatientNumberSequenceAsync(ct);
        }

        if (renumber.Count > 0)
        {
            await StageAsync("patients", renumber, x => x.Id.ToString(), x => Ids.Patient(x.Id), (db, x) => Apply(db, x, false), ct,
                countFetched: false);
            if (!dryRun)
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                foreach (var x in renumber)
                {
                    var number = await db.Patients.Where(p => p.Id == Ids.Patient(x.Id)).Select(p => (int?)p.Number).FirstOrDefaultAsync(ct);
                    if (number is not null)
                    {
                        report.Note($"upstream patient {x.Id}: number {x.Id} was already taken here, imported as number {number}");
                    }
                }
            }
            else
            {
                report.Note($"{renumber.Count} patient(s) would get a new number because theirs is already taken here");
            }
        }
    }

    /// <summary>
    /// The one SQL statement the import runs itself: EF Core has no API for moving an identity
    /// sequence. It is a constant (no interpolation, no input) and only ever moves the sequence
    /// forward, to the highest patient number.
    /// </summary>
    private async Task AdvancePatientNumberSequenceAsync(CancellationToken ct)
    {
        const string sql =
            """
            SELECT setval(x.seq, x.max_number)
            FROM (
                SELECT pg_get_serial_sequence('"Patients"', 'Number')::regclass AS seq,
                       (SELECT MAX("Number") FROM "Patients") AS max_number
            ) AS x
            WHERE x.max_number > COALESCE(pg_sequence_last_value(x.seq), 0)
            """;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    /// <summary>
    /// Stock batches go through the domain's own methods: new batches through
    /// <see cref="MedicationStock.Create"/>, changes through <see cref="MedicationStock.AdjustQuantity"/>
    /// and <see cref="MedicationStock.ChangeStatus"/> (which refuse to break a reservation), with
    /// the same <see cref="StockChangeLog"/> rows the API writes.
    /// </summary>
    private async Task<Outcome> StockAsync(AppDbContext db, UpStock x, CancellationToken ct)
    {
        Require(Ids.Brand(x.MedicationBrandId), "brand", x.MedicationBrandId);
        var (status, remarks, skip) = UpstreamMapper.MapStock(x);
        var id = Ids.Stock(x.Id);
        var stock = await db.MedicationStocks.FindAsync([id], ct);
        if (skip is not null)
        {
            report.Note($"stock {x.Id} not imported: {skip}{(stock is null ? "" : " (the batch imported earlier was left as it is)")}");
            return Outcome.Skipped;
        }

        var location = x.Location.Trim();
        remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        if (stock is null)
        {
            stock = MedicationStock.Create(id, Ids.Brand(x.MedicationBrandId), x.Quantity, x.Expiry, location, status, remarks);
            db.MedicationStocks.Add(stock);
            db.StockChanges.AddRange(StockChangeLog.ForCreate(stock, _importUser, _now));
            return Outcome.Created;
        }

        var before = StockChangeLog.Snapshot.Of(stock);
        stock.MedicationBrandId = Ids.Brand(x.MedicationBrandId);
        stock.Expiry = x.Expiry;
        stock.Location = location;
        stock.Remarks = remarks;
        if (x.Quantity != stock.Quantity)
        {
            stock.AdjustQuantity(x.Quantity - stock.Quantity);
        }

        stock.ChangeStatus(status);
        db.StockChanges.AddRange(StockChangeLog.ForUpdate(before, stock, _importUser, _now));
        return Track(db, stock, isNew: false);
    }

    private void Require(Guid id, string what, object upstreamId)
    {
        if (!_present.Contains(id))
        {
            throw new MappingException($"its {what} {upstreamId} was not imported");
        }
    }

    private static Outcome Track<T>(AppDbContext db, T entity, bool isNew)
        where T : class
    {
        if (isNew)
        {
            db.Add(entity);
            return Outcome.Created;
        }

        var entry = db.Entry(entity);
        entry.DetectChanges();
        return entry.State == EntityState.Modified ? Outcome.Updated : Outcome.Skipped;
    }

    /// <summary>
    /// Maps and saves rows in chunks. When a chunk fails to save, its rows are retried one at a
    /// time so only the offending rows are reported. In a dry run nothing is saved.
    /// </summary>
    private async Task StageAsync<T>(
        string entity,
        IReadOnlyList<T> rows,
        Func<T, string> key,
        Func<T, Guid> id,
        Func<AppDbContext, T, Task<Outcome>> apply,
        CancellationToken ct,
        bool countFetched = true,
        Func<T, bool>? skipWhen = null
    )
    {
        if (countFetched)
        {
            report.Fetched(entity, rows.Count);
        }

        log($"  {entity}: {rows.Count}");
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var chunk in rows.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();
            var staged = new List<(T Row, Outcome Outcome)>();
            foreach (var row in chunk)
            {
                try
                {
                    var outcome = await apply(db, row);
                    if (skipWhen?.Invoke(row) != true)
                    {
                        staged.Add((row, outcome));
                    }
                }
                catch (Exception ex) when (IsRowError(ex))
                {
                    report.Fail(entity, key(row), Describe(ex));
                }
            }

            if (dryRun)
            {
                Commit(entity, staged, id);
                db.ChangeTracker.Clear();
                continue;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                Commit(entity, staged, id);
            }
            catch (DbUpdateException)
            {
                // Find the offending rows: retry one by one on a clean tracker.
                db.ChangeTracker.Clear();
                foreach (var (row, _) in staged)
                {
                    try
                    {
                        var outcome = await apply(db, row);
                        await db.SaveChangesAsync(ct);
                        Commit(entity, [(row, outcome)], id);
                    }
                    catch (Exception ex) when (IsRowError(ex) || ex is DbUpdateException)
                    {
                        report.Fail(entity, key(row), Describe(ex));
                    }
                    finally
                    {
                        db.ChangeTracker.Clear();
                    }
                }
            }

            db.ChangeTracker.Clear();
        }
    }

    private void Commit<T>(string entity, IEnumerable<(T Row, Outcome Outcome)> staged, Func<T, Guid> id)
    {
        foreach (var (row, outcome) in staged)
        {
            report.Record(entity, outcome);
            _present.Add(id(row));
        }
    }

    private static bool IsRowError(Exception ex) =>
        ex is MappingException or ConflictException or BadRequestException or InvalidOperationException;

    private static string Describe(Exception ex) =>
        ex switch
        {
            DbUpdateException { InnerException: { } inner } => inner.Message,
            _ => ex.Message,
        };
}
