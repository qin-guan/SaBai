using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SaBai.API.Domain;
using SaBai.API.Endpoints.Consults.Get;
using SaBai.Tests.Support;

namespace SaBai.Tests.Consults;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class ConsultTests(AppFixture fixture)
{
    [Test]
    public async Task CreateConsult_WithoutDiagnosis_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var visitId = await CreateVisitAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/consults",
            new { visitId, diagnoses = Array.Empty<object>() },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateConsult_IsReturnedOnGetByVisit()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var visitId = await CreateVisitAsync(client);

        // Act
        var created = await client.PostAsJsonAsync(
            "/consults",
            new
            {
                visitId,
                consultation = "cough for 3 days",
                diagnoses = new[]
                {
                    new { details = "Acute cough", category = DiagnosisCategory.Respiratory },
                },
            },
            ApiJson.Options
        );
        var consult = await created.Content.ReadFromJsonAsync<Response>(ApiJson.Options);
        var listed = await client.GetFromJsonAsync<List<Response>>(
            $"/visits/{visitId}/consults",
            ApiJson.Options
        );

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(consult).IsNotNull();
        var match = listed!.SingleOrDefault(x => x.Id == consult!.Id);
        await Assert.That(match).IsNotNull();
        await Assert.That(match!.Consultation).IsEqualTo("cough for 3 days");
        await Assert.That(match.Diagnoses.Any(x => x.Details == "Acute cough")).IsTrue();
        await Assert.That(match.DoctorNickname).IsEqualTo("Admin");
    }

    [Test]
    public async Task PatchConsult_WhenNew_UpdatesText()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var visitId = await CreateVisitAsync(client);
        var consultId = await CreateConsultAsync(client, visitId);

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/consults/{consultId}",
            new { consultation = "updated consultation" },
            ApiJson.Options
        );
        var body = await patched.Content.ReadFromJsonAsync<Response>(ApiJson.Options);

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.Consultation).IsEqualTo("updated consultation");
        await Assert.That(body.PastMedicalHistory).IsEqualTo("asthma");
    }

    [Test]
    public async Task DeleteConsult_ReturnsNoContent()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var visitId = await CreateVisitAsync(client);
        var consultId = await CreateConsultAsync(client, visitId);

        // Act
        var deleted = await client.DeleteAsync($"/consults/{consultId}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task CreateReferral_DefaultsStateToNew()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var visitId = await CreateVisitAsync(client);
        var consultId = await CreateConsultAsync(client, visitId);

        // Act
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new
            {
                consultId,
                referredFor = "Cardiology",
                referralNotes = "soon",
            },
            ApiJson.Options
        );
        var referral =
            await created.Content.ReadFromJsonAsync<SaBai.API.Endpoints.Referrals.Create.Post.Response>(
                ApiJson.Options
            );

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(referral!.ReferralState).IsEqualTo(ReferralState.New);
        await Assert.That(referral.ReferralOutcome).IsNull();
    }

    [Test]
    public async Task UpdateReferralState_SetsStateAndKeepsOmittedOutcome()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var visitId = await CreateVisitAsync(client);
        var consultId = await CreateConsultAsync(client, visitId);
        var created = await client.PostAsJsonAsync(
            "/referrals",
            new { consultId, referredFor = "ENT" },
            ApiJson.Options
        );
        var referral =
            await created.Content.ReadFromJsonAsync<SaBai.API.Endpoints.Referrals.Create.Post.Response>(
                ApiJson.Options
            );

        // Act
        var seen = await client.PatchAsJsonAsync(
            $"/referrals/{referral!.Id}/state",
            new { referralState = ReferralState.Seen, referralOutcome = "booked" },
            ApiJson.Options
        );
        var completed = await client.PatchAsJsonAsync(
            $"/referrals/{referral.Id}/state",
            new { referralState = ReferralState.Completed },
            ApiJson.Options
        );
        var body =
            await completed.Content.ReadFromJsonAsync<SaBai.API.Endpoints.Referrals.UpdateState.Patch.Response>(
                ApiJson.Options
            );

        // Assert
        await Assert.That(seen.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(completed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body!.ReferralState).IsEqualTo(ReferralState.Completed);
        await Assert.That(body.ReferralOutcome).IsEqualTo("booked");
    }

    [Test]
    public async Task PatientRecords_WithoutVisitId_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/patient-records");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task PatientConsult_WithoutVisitId_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/patient-consult");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ConsultPdf_StartsWithPdfHeader()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var visitId = await CreateVisitAsync(client);
        var consultId = await CreateConsultAsync(client, visitId);

        // Act
        var pdf = await client.GetAsync($"/consults/{consultId}/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();

        // Assert
        var header =
            bytes.Length >= 4
                ? Encoding.ASCII.GetString(bytes, 0, 4)
                : Encoding.ASCII.GetString(bytes);
        await Assert.That(pdf.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(pdf.Content.Headers.ContentType?.MediaType).IsEqualTo("application/pdf");
        await Assert.That(header).IsEqualTo("%PDF");
    }

    private static async Task<int> CreateVisitAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var village = await client.PostAsJsonAsync(
            "/village-codes",
            new
            {
                code = $"V{suffix}",
                name = $"Village {suffix}",
                colorHex = "#123456",
            },
            ApiJson.Options
        );
        var villageId = await ReadRootIdAsync(village, "id");

        var patient = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name = $"Patient {suffix}",
                gender = Gender.Female,
                dateOfBirth = new DateTimeOffset(1990, 5, 1, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "None",
                villageCodeId = villageId,
                contactNo = suffix,
                identificationNumber = suffix,
                hasPoorCard = false,
                hasBS2Card = false,
                hasSabaiCard = false,
                toGetReport = false,
            },
            ApiJson.Options
        );
        return await ReadVisitIdAsync(patient);
    }

    private static async Task<int> CreateConsultAsync(HttpClient client, int visitId)
    {
        var response = await client.PostAsJsonAsync(
            "/consults",
            new
            {
                visitId,
                pastMedicalHistory = "asthma",
                consultation = "initial note",
                diagnoses = new[]
                {
                    new { details = "Wheeze", category = DiagnosisCategory.Respiratory },
                },
            },
            ApiJson.Options
        );
        return await ReadRootIdAsync(response, "id");
    }

    private static async Task<int> ReadRootIdAsync(HttpResponseMessage response, string name)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} failed {(int)response.StatusCode}: {body}"
            );
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (TryReadProperty(document.RootElement, name, out var id))
        {
            return id;
        }

        var last = response.Headers.Location?.Segments.LastOrDefault()?.Trim('/');
        if (int.TryParse(last, out id))
        {
            return id;
        }

        throw new InvalidOperationException($"Response did not include {name}: {body}");
    }

    private static async Task<int> ReadVisitIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"POST /patients failed {(int)response.StatusCode}: {body}"
            );
        }

        using var document = JsonDocument.Parse(body);
        if (TryFindInt(document.RootElement, "visitId", out var visitId))
        {
            return visitId;
        }

        if (
            document.RootElement.ValueKind == JsonValueKind.Object
            && TryGetProperty(document.RootElement, "visit", out var visit)
            && TryReadProperty(visit, "id", out visitId)
        )
        {
            return visitId;
        }

        throw new InvalidOperationException($"POST /patients did not return visitId: {body}");
    }

    private static bool TryReadProperty(JsonElement element, string name, out int value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (
                    property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && property.Value.TryGetInt32(out value)
                )
                {
                    return true;
                }
            }
        }

        value = 0;
        return false;
    }

    private static bool TryFindInt(JsonElement element, string name, out int value)
    {
        if (TryReadProperty(element, name, out value))
        {
            return true;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (TryFindInt(property.Value, name, out value))
                    {
                        return true;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindInt(item, name, out value))
                    {
                        return true;
                    }
                }

                break;
        }

        value = 0;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
