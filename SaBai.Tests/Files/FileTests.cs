using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Files;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class FileTests(AppFixture fixture)
{
    [Test]
    public async Task Upload_SoftDelete_HidesFromDefaultList_UntilRestore()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var patientId = await CreatePatientAsync(client, suffix);
        var fileName = $"note-{suffix}.txt";
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(patientId.ToString()), "patientId");
        var bytes = new ByteArrayContent([.. "clinic note"u8]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(bytes, "files", fileName);
        content.Add(new StringContent("visit note"), "descriptions");

        // Act
        var uploaded = await client.PostAsync("/files", content);
        var uploadBody = await uploaded.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<List<FileDto>>(uploadBody, ApiJson.Options);
        var downloaded = await client.GetAsync($"/files/{created![0].Id}/content");
        var downloadedBody = await downloaded.Content.ReadAsStringAsync();
        var listed = await client.GetAsync($"/files?patientId={patientId}");
        var listedBody = await listed.Content.ReadAsStringAsync();
        var deleted = await client.DeleteAsync($"/files/{created[0].Id}");
        var hidden = await client.GetAsync($"/files?patientId={patientId}");
        var hiddenBody = await hidden.Content.ReadAsStringAsync();
        var hiddenContent = await client.GetAsync($"/files/{created[0].Id}/content");
        var restored = await client.PostAsync($"/files/{created[0].Id}/restore", null);
        var visible = await client.GetAsync($"/files?patientId={patientId}");
        var visibleBody = await visible.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(uploaded.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(uploadBody.TrimStart().StartsWith('[')).IsTrue();
        await Assert.That(uploadBody).Contains(fileName);
        await Assert.That(downloaded.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(downloadedBody).IsEqualTo("clinic note");
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(listedBody).Contains(fileName);
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(hidden.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(hiddenBody.Contains(fileName)).IsFalse();
        await Assert.That(hiddenContent.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(restored.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(visibleBody).Contains(fileName);
        var restoredContent = await client.GetAsync($"/files/{created[0].Id}/content");
        await Assert.That(restoredContent.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await restoredContent.Content.ReadAsStringAsync()).IsEqualTo("clinic note");
    }

    [Test]
    public async Task Upload_WithoutFile_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client, Guid.NewGuid().ToString("N")[..8]);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(patientId.ToString()), "patientId");
        content.Add(new StringContent("missing file"), "descriptions");

        // Act
        var response = await client.PostAsync("/files", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static async Task<int> CreatePatientAsync(HttpClient client, string suffix)
    {
        var code = $"F{suffix.ToUpperInvariant()}";
        var village = await client.PostAsJsonAsync(
            "/village-codes",
            new { code, name = $"File village {suffix}", colorHex = "#112233" },
            ApiJson.Options);
        village.EnsureSuccessStatusCode();
        var villageId = ReadInt(await village.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
        var patient = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name = $"File patient {suffix}",
                gender = "male",
                dateOfBirth = new DateTimeOffset(1988, 6, 1, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "None",
                hasPoorCard = false,
                hasBS2Card = false,
                hasSabaiCard = false,
                villageCodeId = villageId,
            },
            ApiJson.Options);
        patient.EnsureSuccessStatusCode();
        return ReadInt(await patient.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
    }

    private static int ReadInt(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.TryGetInt32(out var id))
        {
            return id;
        }

        throw new InvalidOperationException($"Response did not include {name}: {element}");
    }

    private sealed record FileDto(int Id, string FileName, bool IsDeleted);
}
