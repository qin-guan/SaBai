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
