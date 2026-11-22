using System.Text.Json;
using System.Text.Json.Nodes;
using SaBai.API.Domain;
using SaBai.Import.Importing;
using SaBai.Import.Upstream;

namespace SaBai.Tests.Import;

/// <summary>Upstream rows (from recorded-shape fixtures) map onto the SaBai model.</summary>
public sealed class UpstreamMappingTests
{
    private static readonly JsonObject Dataset = FakeUpstream.LoadDataset(new Uri("https://images.example.org"));

    private static T Rows<T>(string procedure) => Dataset[procedure].Deserialize<T>(UpstreamClient.Json)!;

    [Test]
    public async Task RecordedPatientsResponse_UnwrapsSuperjsonAndMaps()
    {
        var body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", "patients-list.response.json"));
        var up = UpstreamClient.Unwrap("patientsRouter.list", HttpStatusCode.OK, body)
            .Deserialize<List<UpPatient>>(UpstreamClient.Json)!
            .Single();
        var village = Guid.CreateVersion7();
        var patient = new Patient { Name = "", DrugAllergy = "" };

        UpstreamMapper.Apply(up, patient, village, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

        await Assert.That(patient.Name).IsEqualTo("Sok Dara");
        await Assert.That(patient.IdentificationNumber).IsEqualTo("ID-42");
        await Assert.That(patient.ContactNo).IsNull();
        await Assert.That(patient.Gender).IsEqualTo(Gender.Female);
        await Assert.That(patient.DateOfBirth).IsEqualTo(new DateTimeOffset(2014, 5, 6, 0, 0, 0, TimeSpan.Zero));
        await Assert.That(patient.DrugAllergy).IsEqualTo("Penicillin");
        await Assert.That(patient.HasPoorCard).IsTrue();
        await Assert.That(patient.HasSabaiCard).IsTrue();
        await Assert.That(patient.VillageCodeId).IsEqualTo(village);
        // Photo and face are the media phase's job.
        await Assert.That(patient.PhotoStoredFileName).IsNull();
        await Assert.That(patient.LatestFaceId).IsNull();
        await Assert.That(up.PatientImageUrl).IsEqualTo("https://res.cloudinary.com/demo/image/upload/v1700000001/abc");
    }

    [Test]
    public async Task Vitals_ParseNumericStringsAndLinkToTheVisit()
    {
        var up = Rows<Dictionary<string, UpVitals?>>("vitalsRouter.getByVisitId")["720001"]!;
        var visitDate = DateTimeOffset.Parse("2025-06-01T02:00:00Z");
        var vitals = new global::SaBai.API.Domain.Vitals();

        UpstreamMapper.Apply(up, vitals, visitDate);

        await Assert.That(vitals.VisitId).IsEqualTo(DeterministicIds.Visit(720001));
        await Assert.That(vitals.Height).IsEqualTo(120.50m);
        await Assert.That(vitals.Weight).IsEqualTo(23.40m);
        await Assert.That(vitals.BloodGlucoseNonFasting).IsNull();
        await Assert.That(vitals.Systolic).IsEqualTo(110);
        await Assert.That(vitals.RecordedAt).IsEqualTo(visitDate);
        Assert.Throws<MappingException>(() => UpstreamMapper.Decimal("12,5kg", "weight"));
    }

    [Test]
    public async Task Consults_KeepDoctorAsPlaceholderAndMapDiagnosisCategories()
    {
        var up = Rows<Dictionary<string, List<UpConsult>>>("consultsRouter.getByVisitId")["720001"].Single();
        var consult = new Consult();
        var doctor = Guid.CreateVersion7();
        UpstreamMapper.Apply(up, consult, doctor);

        var mapped = up.Diagnoses!.Select(d =>
        {
            var diagnosis = new Diagnosis { Details = "" };
            UpstreamMapper.Apply(d, diagnosis);
            return diagnosis;
        }).ToList();

        await Assert.That(consult.DoctorId).IsEqualTo(doctor);
        await Assert.That(consult.VisitId).IsEqualTo(DeterministicIds.Visit(720001));
        await Assert.That(mapped[0].Category).IsEqualTo(DiagnosisCategory.Respiratory);
        // Unknown text is kept: category Others, original text in the details.
        await Assert.That(mapped[1].Category).IsEqualTo(DiagnosisCategory.Others);
        await Assert.That(mapped[1].Details).IsEqualTo("Rash (category: Skin stuff)");
        await Assert.That(mapped[2].Category).IsEqualTo(DiagnosisCategory.EarNoseThroat);
    }

    [Test]
    public async Task Referrals_MapStateAndRejectUnknownOnes()
    {
        var up = Rows<List<UpReferral>>("referralRouter.list").Single();
        var referral = new Referral { ReferredFor = "" };
        UpstreamMapper.Apply(up, referral);

        await Assert.That(referral.ReferralState).IsEqualTo(ReferralState.Seen);
        await Assert.That(referral.ConsultId).IsEqualTo(DeterministicIds.Consult(760003));
        Assert.Throws<MappingException>(() =>
            UpstreamMapper.Apply(up with { ReferralState = "Lost" }, new Referral { ReferredFor = "" })
        );
    }

    [Test]
    public async Task Stock_StatusesMapOntoTheReservationModel()
    {
        var stock = Rows<List<UpStock>>("medicationStockRouter.list").ToDictionary(x => x.Id);

        var active = UpstreamMapper.MapStock(stock[790201]);
        var reserved = UpstreamMapper.MapStock(stock[790202]);
        var dispensed = UpstreamMapper.MapStock(stock[790203]);
        var discarded = UpstreamMapper.MapStock(stock[790204]);

        await Assert.That(active).IsEqualTo((StockStatus.Active, (string?)null, (string?)null));
        // Reserved for an order we cannot read: on hand, so active, and labelled.
        await Assert.That(reserved.Status).IsEqualTo(StockStatus.Active);
        await Assert.That(reserved.Remarks).IsEqualTo(UpstreamMapper.ReservedRemark);
        await Assert.That(reserved.SkipReason).IsNull();
        // Dispensed units have left the shelf: not imported.
        await Assert.That(dispensed.SkipReason).IsNotNull();
        await Assert.That(discarded).IsEqualTo((StockStatus.Discarded, (string?)"Expired", (string?)null));
        Assert.Throws<MappingException>(() => UpstreamMapper.MapStock(stock[790201] with { StockStatus = "lost" }));
    }

    [Test]
    public async Task Broadcasts_UseSenderNameAndSeverity()
    {
        var up = Rows<List<UpBroadcast>>("broadcastsRouter.list").Single();
        var broadcast = new Broadcast { Title = "", Body = "" };
        UpstreamMapper.Apply(up, broadcast, up.SenderName);

        await Assert.That(broadcast.Severity).IsEqualTo(BroadcastSeverity.Warning);
        await Assert.That(broadcast.CreatedBy).IsEqualTo("Dr Alice");
        await Assert.That(broadcast.PublishedAt).IsEqualTo(DateTimeOffset.Parse("2025-06-01T01:00:01Z"));
    }

    [Test]
    public async Task Gender_RejectsUnknownValues()
    {
        await Assert.That(UpstreamMapper.ParseGender("male")).IsEqualTo(Gender.Male);
        Assert.Throws<MappingException>(() => UpstreamMapper.ParseGender("x"));
    }

    [Test]
    public async Task DeterministicIds_AreStableVersion7AndOrderedLikeUpstream()
    {
        var a = DeterministicIds.Patient(42);

        await Assert.That(DeterministicIds.Patient(42)).IsEqualTo(a);
        await Assert.That(DeterministicIds.Visit(42)).IsNotEqualTo(a);
        await Assert.That(a.Version).IsEqualTo(7);
        // Pinned: changing the scheme would orphan every imported row.
        await Assert.That(a.ToString()).IsEqualTo(PinnedPatient42);
        // SaBai lists order by id as creation order: imported rows keep upstream's order and
        // sort before anything created in SaBai.
        await Assert.That(string.CompareOrdinal(DeterministicIds.Broadcast(7).ToString(), DeterministicIds.Broadcast(8).ToString()))
            .IsLessThan(0);
        await Assert.That(DeterministicIds.Broadcast(int.MaxValue).CompareTo(Guid.CreateVersion7())).IsLessThan(0);
        await Assert.That(string.CompareOrdinal(DeterministicIds.Broadcast(int.MaxValue).ToString(), Guid.CreateVersion7().ToString()))
            .IsLessThan(0);
    }

    // Epoch 2020-01-01T00:00:00Z + 42 ms, then SHA-256("patients:42") bits (computed independently).
    private const string PinnedPatient42 = "016f5e66-e82a-7dbe-85d8-dedc19aab741";
}
