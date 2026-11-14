using System.Net.Http.Json;
using System.Text.Json;
using SaBai.API.Domain;
using SaBai.Tests.Support;
using AuditLogResponse = SaBai.API.Endpoints.AuditLogs.List.Get.Response;

namespace SaBai.Tests.Offline;

/// <summary>
/// What a device replaying queued writes relies on: creates take the client's id and can be
/// resent safely, and <c>If-Unmodified-Since</c> refuses to overwrite another user's later edit.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class OfflineReplayTests(AppFixture fixture)
{
    [Test]
    public async Task RegisterPatient_WithClientIds_UsesThem()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, visitId, vitalsId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        // Act
        var response = await client.PostAsJsonAsync(
            "/patients",
            Registration(villageId, patientId, visitId, vitalsId),
            ApiJson.Options
        );
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var vitals = await client.GetAsync($"/vitals/{vitalsId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(ClinicSetup.ReadGuid(body, "id")).IsEqualTo(patientId);
        await Assert.That(ClinicSetup.ReadGuid(body, "visitId")).IsEqualTo(visitId);
        await Assert.That(ClinicSetup.ReadGuid(body, "vitalsId")).IsEqualTo(vitalsId);
        await Assert.That(vitals.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task RegisterPatient_Resent_ReturnsSamePatientWithoutDuplicating()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, visitId, vitalsId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var request = Registration(villageId, patientId, visitId, vitalsId);
        var first = await client.PostAsJsonAsync("/patients", request, ApiJson.Options);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Act
        var second = await client.PostAsJsonAsync("/patients", request, ApiJson.Options);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var visits = await client.GetFromJsonAsync<JsonElement>(
            $"/patients/{patientId}/visits",
            ApiJson.Options
        );

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(ClinicSetup.ReadGuid(secondBody, "id")).IsEqualTo(patientId);
        await Assert.That(ClinicSetup.ReadGuid(secondBody, "visitId")).IsEqualTo(visitId);
        await Assert.That(ClinicSetup.ReadGuid(secondBody, "vitalsId")).IsEqualTo(vitalsId);
        await Assert.That(secondBody.GetProperty("displayId").GetString())
            .IsEqualTo(firstBody.GetProperty("displayId").GetString());
        await Assert.That(visits.GetProperty("visits").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task CreateVitals_Resent_ReturnsSameRecord()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var id = Guid.CreateVersion7();
        var request = new { id, visitId, temperature = 36.6m };
        var first = await client.PostAsJsonAsync("/vitals", request, ApiJson.Options);

        // Act
        var second = await client.PostAsJsonAsync("/vitals", request, ApiJson.Options);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(ClinicSetup.ReadGuid(body, "id")).IsEqualTo(id);
        await Assert.That(body.GetProperty("temperature").GetDecimal()).IsEqualTo(36.6m);
    }

    [Test]
    public async Task CreateVitals_IdUsedOnAnotherVisit_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, firstVisitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var (_, secondVisitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var id = Guid.CreateVersion7();
        (await client.PostAsJsonAsync("/vitals", new { id, visitId = firstVisitId }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { id, visitId = secondVisitId },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Create_EmptyClientId_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);

        // Act
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { id = Guid.Empty, visitId },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Update_IfUnmodifiedSinceBeforeOtherUsersEdit_ReturnsPreconditionFailed()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var vitalsId = await CreateVitalsAsync(admin);
        var queuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        (await doctor.PatchAsJsonAsync($"/vitals/{vitalsId}", new { temperature = 37.2m }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await PatchSinceAsync(admin, vitalsId, 38.0m, queuedAt);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var current = await admin.GetFromJsonAsync<JsonElement>($"/vitals/{vitalsId}", ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.PreconditionFailed);
        await Assert.That(body.GetProperty("status").GetInt32()).IsEqualTo(412);
        await Assert.That(body.GetProperty("recordType").GetString()).IsEqualTo("Vitals");
        await Assert.That(body.GetProperty("recordId").GetString()).IsEqualTo(vitalsId.ToString());
        await Assert.That(current.GetProperty("temperature").GetDecimal()).IsEqualTo(37.2m);
    }

    [Test]
    public async Task Update_IfUnmodifiedSinceAfterOwnEdit_Succeeds()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var vitalsId = await CreateVitalsAsync(admin);
        var queuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        (await admin.PatchAsJsonAsync($"/vitals/{vitalsId}", new { temperature = 37.2m }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await PatchSinceAsync(admin, vitalsId, 38.0m, queuedAt);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Update_WithoutIfUnmodifiedSince_OverwritesOtherUsersEdit()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var vitalsId = await CreateVitalsAsync(admin);
        (await doctor.PatchAsJsonAsync($"/vitals/{vitalsId}", new { temperature = 37.2m }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await admin.PatchAsJsonAsync(
            $"/vitals/{vitalsId}",
            new { temperature = 38.0m },
            ApiJson.Options
        );
        var current = await admin.GetFromJsonAsync<JsonElement>($"/vitals/{vitalsId}", ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(current.GetProperty("temperature").GetDecimal()).IsEqualTo(38.0m);
    }

    [Test]
    public async Task AdjustStock_Resent_AppliesOnce()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var stockId = await CreateStockAsync(client, quantity: 5);
        var request = new { adjustmentId = Guid.CreateVersion7(), quantityChange = 2 };
        var first = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/adjustments",
            request,
            ApiJson.Options
        );

        // Act
        var second = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/adjustments",
            request,
            ApiJson.Options
        );
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(firstBody.GetProperty("quantity").GetInt32()).IsEqualTo(7);
        await Assert.That(secondBody.GetProperty("quantity").GetInt32()).IsEqualTo(7);
    }

    [Test]
    public async Task AdjustStock_IdUsedOnAnotherLot_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var firstStockId = await CreateStockAsync(client, quantity: 5);
        var secondStockId = await CreateStockAsync(client, quantity: 5);
        var adjustmentId = Guid.CreateVersion7();
        (await client.PostAsJsonAsync(
            $"/medication-stock/{firstStockId}/adjustments",
            new { adjustmentId, quantityChange = 1 },
            ApiJson.Options
        )).EnsureSuccessStatusCode();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/medication-stock/{secondStockId}/adjustments",
            new { adjustmentId, quantityChange = 1 },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Update_AfterOtherUserChangedOnlyTags_ReturnsPreconditionFailed()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var villageId = await ClinicSetup.CreateVillageAsync(admin);
        var (patientId, _) = await ClinicSetup.RegisterPatientAsync(admin, villageId);
        var queuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        (await doctor.PatchAsJsonAsync(
            $"/patients/{patientId}",
            new { tags = new[] { $"Follow-Up {ClinicSetup.Suffix()}" } },
            ApiJson.Options
        )).EnsureSuccessStatusCode();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/patients/{patientId}")
        {
            Content = JsonContent.Create(new { contactNo = "012 345 678" }, options: ApiJson.Options),
        };
        request.Headers.IfUnmodifiedSince = queuedAt;
        var response = await admin.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.PreconditionFailed);
        await Assert.That(body.GetProperty("recordType").GetString()).IsEqualTo("Patient");
    }

    [Test]
    public async Task Update_OtherUsersEditInTheSameSecondAsTheHeader_ReturnsPreconditionFailed()
    {
        // Arrange
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var villageId = await ClinicSetup.CreateVillageAsync(admin);
        var edited = await doctor.PatchAsJsonAsync(
            $"/village-codes/{villageId}",
            new { name = $"Renamed {ClinicSetup.Suffix()}" },
            ApiJson.Options
        );
        edited.EnsureSuccessStatusCode();
        var changedAt = (await edited.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options))
            .GetProperty("updatedAt")
            .GetDateTimeOffset();
        // HTTP dates have whole seconds: the header names the second the other edit happened in.
        var sameSecond = DateTimeOffset.FromUnixTimeSeconds(changedAt.ToUnixTimeSeconds());

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/village-codes/{villageId}")
        {
            Content = JsonContent.Create(new { name = $"Mine {ClinicSetup.Suffix()}" }, options: ApiJson.Options),
        };
        request.Headers.IfUnmodifiedSince = sameSecond;
        var response = await admin.SendAsync(request);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task CreateVitals_Resent_RecordsAReadInTheAuditLog()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var id = Guid.CreateVersion7();
        var request = new { id, visitId, temperature = 36.6m };
        (await client.PostAsJsonAsync("/vitals", request, ApiJson.Options)).EnsureSuccessStatusCode();

        // Act
        var resent = await client.PostAsJsonAsync("/vitals", request, ApiJson.Options);
        var log = await client.GetFromJsonAsync<AuditLogResponse>(
            $"/audit-logs?entityType=Vitals&entityId={id}&action=Read",
            ApiJson.Options
        );

        // Assert
        await Assert.That(resent.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var read = log!.Entries.Single();
        await Assert.That(read.Action).IsEqualTo(AuditAction.Read);
        await Assert.That(read.Request).IsEqualTo("POST /vitals");
    }

    [Test]
    public async Task RegisterPatient_ResentForAnotherRegistration_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var patientId = Guid.CreateVersion7();
        (await client.PostAsJsonAsync(
            "/patients",
            Registration(villageId, patientId, Guid.CreateVersion7(), Guid.CreateVersion7()),
            ApiJson.Options
        )).EnsureSuccessStatusCode();

        // Act: the same patient id with a visit it never opened, and without a visit for someone else.
        var otherVisit = await client.PostAsJsonAsync(
            "/patients",
            Registration(villageId, patientId, Guid.CreateVersion7(), Guid.CreateVersion7()),
            ApiJson.Options
        );
        var otherPerson = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                id = patientId,
                name = $"Someone else {ClinicSetup.Suffix()}",
                gender = "male",
                dateOfBirth = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "None",
                villageCodeId = villageId,
            },
            ApiJson.Options
        );
        var visits = await client.GetFromJsonAsync<JsonElement>(
            $"/patients/{patientId}/visits",
            ApiJson.Options
        );

        // Assert
        await Assert.That(otherVisit.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(otherPerson.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(visits.GetProperty("visits").GetArrayLength()).IsEqualTo(1);
    }

    [Test]
    public async Task CreateVisit_DatedInTheFuture_IsDatedNow()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, _) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var past = DateTimeOffset.UtcNow.AddHours(-2);

        // Act
        var future = await client.PostAsJsonAsync(
            "/visits",
            new { patientId, villageCodeId = villageId, date = DateTimeOffset.UtcNow.AddDays(3) },
            ApiJson.Options
        );
        var earlier = await client.PostAsJsonAsync(
            "/visits",
            new { patientId, villageCodeId = villageId, date = past },
            ApiJson.Options
        );
        var futureDate = (await future.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options))
            .GetProperty("date")
            .GetDateTimeOffset();
        var earlierDate = (await earlier.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options))
            .GetProperty("date")
            .GetDateTimeOffset();

        // Assert
        await Assert.That(future.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(futureDate <= DateTimeOffset.UtcNow.AddMinutes(1)).IsTrue();
        await Assert.That(Math.Abs((earlierDate - past).TotalSeconds) < 1).IsTrue();
    }

    [Test]
    public async Task CreateVitals_SentTwiceAtOnce_BothSucceedWithOneRecord()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var id = Guid.CreateVersion7();
        var request = new { id, visitId, temperature = 36.6m };

        // Act: a resend while the first request is still being saved.
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/vitals", request, ApiJson.Options),
            client.PostAsJsonAsync("/vitals", request, ApiJson.Options)
        );
        var saved = await client.GetFromJsonAsync<JsonElement>($"/visits/{visitId}/vitals", ApiJson.Options);

        // Assert
        await Assert.That(responses.All(x => x.StatusCode == HttpStatusCode.Created)).IsTrue();
        await Assert.That(ClinicSetup.ReadGuid(saved, "id")).IsEqualTo(id);
    }

    private static async Task<Guid> CreateStockAsync(HttpClient client, int quantity)
    {
        var suffix = ClinicSetup.Suffix();
        var ingredient = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new { name = $"Ingredient {suffix}", unitOfMeasurement = "mg", fallBelow = 1 },
            ApiJson.Options
        );
        ingredient.EnsureSuccessStatusCode();
        var brand = await client.PostAsJsonAsync(
            "/medication-brands",
            new
            {
                name = $"Brand {suffix}",
                activeIngredientId = ClinicSetup.ReadGuid(
                    await ingredient.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
                    "id"
                ),
                remarks = "",
            },
            ApiJson.Options
        );
        brand.EnsureSuccessStatusCode();
        var stock = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = ClinicSetup.ReadGuid(
                    await brand.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
                    "id"
                ),
                quantity,
                expiry = DateTimeOffset.UtcNow.AddYears(1),
                location = $"shelf-{suffix}",
                stockStatus = "active",
            },
            ApiJson.Options
        );
        stock.EnsureSuccessStatusCode();
        return ClinicSetup.ReadGuid(
            await stock.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static object Registration(Guid villageId, Guid id, Guid visitId, Guid vitalsId) =>
        new
        {
            id,
            visitId,
            vitalsId,
            name = $"Patient {ClinicSetup.Suffix()}",
            gender = "female",
            dateOfBirth = new DateTimeOffset(1991, 3, 4, 0, 0, 0, TimeSpan.Zero),
            drugAllergy = "None",
            villageCodeId = villageId,
            vitals = new { temperature = 36.5m },
        };

    private static async Task<Guid> CreateVitalsAsync(HttpClient client)
    {
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var response = await client.PostAsJsonAsync(
            "/vitals",
            new { visitId, temperature = 36.6m },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return ClinicSetup.ReadGuid(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static async Task<HttpResponseMessage> PatchSinceAsync(
        HttpClient client,
        Guid vitalsId,
        decimal temperature,
        DateTimeOffset since
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/vitals/{vitalsId}")
        {
            Content = JsonContent.Create(new { temperature }, options: ApiJson.Options),
        };
        request.Headers.IfUnmodifiedSince = since;
        return await client.SendAsync(request);
    }
}
