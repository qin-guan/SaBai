using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Consults;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class ReferralTests(AppFixture fixture)
{
    [Test]
    public async Task Create_DefaultsToNew_AndGetIncludesPatient()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var patientName = $"Referral {suffix}";
        var (consultId, _) = await CreateConsultAsync(client, patientName);

        // Act
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = "eye clinic" },
            ApiJson.Options
        );
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var id = ClinicSetup.ReadGuid(createdBody, "id");
        var listed = await client.GetAsync($"/consults/{consultId}/referrals");
        var items = await listed.Content.ReadFromJsonAsync<List<JsonElement>>(ApiJson.Options);
        var fetched = await client.GetAsync($"/referrals/{id}");
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(createdBody.GetProperty("referralState").GetString()).IsEqualTo("New");
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(items!.Any(x => ClinicSetup.ReadGuid(x, "id") == id)).IsTrue();
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("patientName").GetString()).IsEqualTo(patientName);
        await Assert.That(body.GetProperty("hasPhoto").GetBoolean()).IsFalse();
        await Assert.That(ClinicSetup.ReadGuid(body, "villageCodeId")).IsNotEqualTo(Guid.Empty);
        await Assert.That(body.GetProperty("villageCode").GetString()).IsNotNull();
        await Assert.That(body.GetProperty("villageColorHex").GetString()).IsNotNull();
    }

    [Test]
    public async Task Create_MissingConsult_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId = ClinicSetup.MissingId, referredFor = $"missing {ClinicSetup.Suffix()}" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_EmptyReferredFor_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var (consultId, _) = await CreateConsultAsync(
            client,
            $"Empty refer {ClinicSetup.Suffix()}"
        );

        // Act
        var response = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = "" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Get_Missing_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync($"/referrals/{ClinicSetup.MissingId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateOutcome_KeepsStateAndCanBeCleared()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var (consultId, _) = await CreateConsultAsync(client, $"Outcome {suffix}");
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = $"outcome {suffix}" },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadGuid(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
        (
            await client.PatchAsJsonAsync(
                $"/referrals/{id}/state",
                new { referralState = "Seen" },
                ApiJson.Options
            )
        ).EnsureSuccessStatusCode();

        // Act
        var set = await client.PatchAsJsonAsync(
            $"/referrals/{id}/outcome",
            new { referralOutcome = "booked for Monday" },
            ApiJson.Options
        );
        var setBody = await set.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var cleared = await client.PatchAsJsonAsync(
            $"/referrals/{id}/outcome",
            new { referralOutcome = "" },
            ApiJson.Options
        );
        var clearedBody = await cleared.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(set.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(setBody.GetProperty("referralState").GetString()).IsEqualTo("Seen");
        await Assert
            .That(setBody.GetProperty("referralOutcome").GetString())
            .IsEqualTo("booked for Monday");
        await Assert.That(cleared.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(clearedBody.GetProperty("referralState").GetString()).IsEqualTo("Seen");
        await Assert.That(clearedBody.GetProperty("referralOutcome").GetString()).IsEqualTo("");
    }

    [Test]
    public async Task UpdateOutcome_UnknownReferralOrMissingOutcome_Fails()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();

        // Act
        var unknown = await client.PatchAsJsonAsync(
            $"/referrals/{ClinicSetup.MissingId}/outcome",
            new { referralOutcome = "x" },
            ApiJson.Options
        );
        var missing = await client.PatchAsJsonAsync(
            "/referrals/1/outcome",
            new { },
            ApiJson.Options
        );

        // Assert
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateState_InvalidState_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var (consultId, _) = await CreateConsultAsync(client, $"Bad state {suffix}");
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = $"state {suffix}" },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadGuid(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/referrals/{id}/state",
            new { referralState = "NotAState" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateState_OmitsOutcome_KeepsBooked()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var (consultId, _) = await CreateConsultAsync(client, $"Outcome {suffix}");
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = $"outgoing {suffix}" },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadGuid(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var seen = await client.PatchAsJsonAsync(
            $"/referrals/{id}/state",
            new { referralState = "Seen", referralOutcome = "booked" },
            ApiJson.Options
        );
        var outgoing = await client.PatchAsJsonAsync(
            $"/referrals/{id}/state",
            new { referralState = "Outgoing" },
            ApiJson.Options
        );
        var body = await outgoing.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(seen.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(outgoing.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("referralState").GetString()).IsEqualTo("Outgoing");
        await Assert.That(body.GetProperty("referralOutcome").GetString()).IsEqualTo("booked");
    }

    [Test]
    public async Task ScopedSectionUpdates_DoNotOverwriteOtherSections()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var (consultId, _) = await CreateConsultAsync(client, $"Sections {suffix}");
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = $"cardiology {suffix}" },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadGuid(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        var partnerCreated = await client.PostAsJsonAsync(
            "/referral-partners",
            new { name = $"Partner {suffix}", remarks = "Main hospital", isActive = true },
            ApiJson.Options
        );
        partnerCreated.EnsureSuccessStatusCode();
        var partnerId = (await partnerCreated.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options))
            .GetProperty("id")
            .GetGuid();

        // Act: update booth, then party, then money independently
        var boothRes = await client.PatchAsJsonAsync(
            $"/referrals/{id}/booth",
            new
            {
                id,
                referredToId = partnerId,
                guardiansContacted = "NotApplicable",
                classOfService = "ChronicManagement",
                descriptionOfService = "Echo",
                patientSummaryFromScreening = "Shortness of breath",
                remarksAtBooth = "Bring old ECG",
            },
            ApiJson.Options
        );
        var partyRes = await client.PatchAsJsonAsync(
            $"/referrals/{id}/party",
            new
            {
                id,
                presentAtReferralParty = true,
                referralPartyResults = "Normal sinus rhythm",
                followUp = true,
                doctorsUpdated = true,
            },
            ApiJson.Options
        );
        var moneyRes = await client.PatchAsJsonAsync(
            $"/referrals/{id}/money",
            new
            {
                id,
                amountPaidUsd = 25.50m,
                amountPaidRiels = 40000,
            },
            ApiJson.Options
        );
        var body = await moneyRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(boothRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(partyRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(moneyRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("referredToId").GetGuid()).IsEqualTo(partnerId);
        await Assert.That(body.GetProperty("classOfService").GetString()).IsEqualTo("ChronicManagement");
        await Assert.That(body.GetProperty("presentAtReferralParty").GetBoolean()).IsTrue();
        await Assert.That(body.GetProperty("referralPartyResults").GetString()).IsEqualTo("Normal sinus rhythm");
        await Assert.That(body.GetProperty("amountPaidUsd").GetDecimal()).IsEqualTo(25.50m);
        await Assert.That(body.GetProperty("amountPaidRiels").GetInt32()).IsEqualTo(40000);
    }

    [Test]
    public async Task UpdateMoney_NegativeAmount_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var (consultId, _) = await CreateConsultAsync(client, $"MoneyBad {suffix}");
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = $"imaging {suffix}" },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadGuid(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var res = await client.PatchAsJsonAsync(
            $"/referrals/{id}/money",
            new { id, amountPaidUsd = -5m, amountPaidRiels = 1000 },
            ApiJson.Options
        );

        // Assert
        await Assert.That(res.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateReimbursement_MarksAndUnmarksBill_AndListIncludesFields()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var (consultId, _) = await CreateConsultAsync(client, $"Reimburse {suffix}");
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = $"lab {suffix}" },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadGuid(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
        await client.PatchAsJsonAsync(
            $"/referrals/{id}/money",
            new { id, amountPaidUsd = 15.00m, amountPaidRiels = 20000 },
            ApiJson.Options
        );

        // Act - mark reimbursed
        var markRes = await client.PatchAsJsonAsync(
            $"/referrals/{id}/reimbursement",
            new { id, isReimbursed = true, reimbursementNotes = "Settled in cash" },
            ApiJson.Options
        );
        var markBody = await markRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert - marked
        await Assert.That(markRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(markBody.GetProperty("isReimbursed").GetBoolean()).IsTrue();
        await Assert.That(markBody.GetProperty("reimbursedAt").ValueKind).IsNotEqualTo(JsonValueKind.Null);
        await Assert.That(markBody.GetProperty("reimbursementNotes").GetString()).IsEqualTo("Settled in cash");

        // Act - list includes payment & reimbursement fields
        var listRes = await client.GetAsync("/referrals");
        var listItems = await listRes.Content.ReadFromJsonAsync<List<JsonElement>>(ApiJson.Options);
        var item = listItems!.First(x => ClinicSetup.ReadGuid(x, "id") == id);

        await Assert.That(item.GetProperty("amountPaidUsd").GetDecimal()).IsEqualTo(15.00m);
        await Assert.That(item.GetProperty("amountPaidRiels").GetInt32()).IsEqualTo(20000);
        await Assert.That(item.GetProperty("isReimbursed").GetBoolean()).IsTrue();

        // Act - unmark
        var unmarkRes = await client.PatchAsJsonAsync(
            $"/referrals/{id}/reimbursement",
            new { id, isReimbursed = false, reimbursementNotes = (string?)null },
            ApiJson.Options
        );
        var unmarkBody = await unmarkRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        await Assert.That(unmarkRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(unmarkBody.GetProperty("isReimbursed").GetBoolean()).IsFalse();
        await Assert.That(unmarkBody.GetProperty("reimbursedAt").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task ReimburseBills_BulkUpdatesMultipleReferrals()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var (consult1, _) = await CreateConsultAsync(client, $"Bulk1 {suffix}");
        var (consult2, _) = await CreateConsultAsync(client, $"Bulk2 {suffix}");

        var r1 = await client.PostAsJsonAsync("/referrals", new { consultId = consult1, referredFor = $"x1 {suffix}" }, ApiJson.Options);
        var r2 = await client.PostAsJsonAsync("/referrals", new { consultId = consult2, referredFor = $"x2 {suffix}" }, ApiJson.Options);
        var id1 = ClinicSetup.ReadGuid(await r1.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
        var id2 = ClinicSetup.ReadGuid(await r2.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");

        // Act
        var bulkRes = await client.PostAsJsonAsync(
            "/referrals/bills/reimburse",
            new { referralIds = new[] { id1, id2 }, isReimbursed = true, reimbursementNotes = "Batch #1" },
            ApiJson.Options
        );
        var bulkBody = await bulkRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(bulkRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(bulkBody.GetProperty("updatedCount").GetInt32()).IsEqualTo(2);
        await Assert.That(bulkBody.GetProperty("isReimbursed").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task PartialRepayment_BillAndPnLumpSum_AllocatesFifo()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();

        var meRes = await client.GetAsync("/auth/whoami");
        var meBody = await meRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var memberId = ClinicSetup.ReadGuid(meBody.GetProperty("user"), "id");

        var (c1, _) = await CreateConsultAsync(client, $"Partial1 {suffix}");
        var (c2, _) = await CreateConsultAsync(client, $"Partial2 {suffix}");

        var r1 = await client.PostAsJsonAsync("/referrals", new { consultId = c1, referredFor = $"p1 {suffix}" }, ApiJson.Options);
        var r2 = await client.PostAsJsonAsync("/referrals", new { consultId = c2, referredFor = $"p2 {suffix}" }, ApiJson.Options);
        var id1 = ClinicSetup.ReadGuid(await r1.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
        var id2 = ClinicSetup.ReadGuid(await r2.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");

        await client.PatchAsJsonAsync($"/referrals/{id1}/money", new { id = id1, amountPaidUsd = 20.00m, amountPaidRiels = 0, memberWhoPaidId = memberId }, ApiJson.Options);
        await client.PatchAsJsonAsync($"/referrals/{id2}/money", new { id = id2, amountPaidUsd = 30.00m, amountPaidRiels = 0, memberWhoPaidId = memberId }, ApiJson.Options);

        // Act 1: partial repayment on bill 1 ($5 out of $20)
        var partialRes = await client.PatchAsJsonAsync(
            $"/referrals/{id1}/reimbursement",
            new { id = id1, isReimbursed = false, reimbursedUsd = 5.00m, reimbursementNotes = "Part 1" },
            ApiJson.Options
        );
        var partialBody = await partialRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        await Assert.That(partialRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(partialBody.GetProperty("isReimbursed").GetBoolean()).IsFalse();
        await Assert.That(partialBody.GetProperty("reimbursedUsd").GetDecimal()).IsEqualTo(5.00m);

        // Act 2: lump-sum partial repayment of $25 to the PN ($15 finishes bill 1, $10 goes to bill 2)
        var repayRes = await client.PostAsJsonAsync(
            "/referrals/bills/repay-pn",
            new { memberWhoPaidId = memberId, amountUsd = 25.00m, amountRiels = 0, reimbursementNotes = "EOD cash" },
            ApiJson.Options
        );
        var repayBody = await repayRes.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        await Assert.That(repayRes.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(repayBody.GetProperty("updatedCount").GetInt32()).IsEqualTo(2);
        await Assert.That(repayBody.GetProperty("appliedUsd").GetDecimal()).IsEqualTo(25.00m);

        var b1 = await (await client.GetAsync($"/referrals/{id1}")).Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var b2 = await (await client.GetAsync($"/referrals/{id2}")).Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        await Assert.That(b1.GetProperty("isReimbursed").GetBoolean()).IsTrue();
        await Assert.That(b1.GetProperty("reimbursedUsd").GetDecimal()).IsEqualTo(20.00m);
        await Assert.That(b2.GetProperty("isReimbursed").GetBoolean()).IsFalse();
        await Assert.That(b2.GetProperty("reimbursedUsd").GetDecimal()).IsEqualTo(10.00m);
    }

    private static async Task<(Guid ConsultId, Guid VisitId)> CreateConsultAsync(
        HttpClient client,
        string patientName
    )
    {
        var suffix = ClinicSetup.Suffix();
        var villageId = await ClinicSetup.CreateVillageAsync(client, $"F{suffix}");
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId, patientName);
        var consultId = await ClinicSetup.CreateConsultAsync(client, visitId, $"refer {suffix}");
        return (consultId, visitId);
    }
}
