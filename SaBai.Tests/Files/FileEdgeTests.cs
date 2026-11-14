using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Files;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class FileEdgeTests(AppFixture fixture)
{
    [Test]
    public async Task Upload_UnknownPatient_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        using var content = Form(ClinicSetup.MissingId, $"orphan-{ClinicSetup.Suffix()}.pdf", "orphan note");

        // Act
        var response = await client.PostAsync("/files", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Upload_DescriptionCountMismatch_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        var suffix = ClinicSetup.Suffix();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(patientId.ToString()), "patientId");
        content.Add(FilePart("one"), "files", $"a-{suffix}.pdf");
        content.Add(FilePart("two"), "files", $"b-{suffix}.pdf");
        content.Add(new StringContent("only one"), "descriptions");

        // Act
        var response = await client.PostAsync("/files", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task List_InvalidDeleted_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);

        // Act
        var response = await client.GetAsync("/files?deleted=maybe");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);

        // Act
        var response = await client.GetAsync($"/files/{ClinicSetup.MissingId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Download_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        var fileId = await UploadAsync(
            client,
            patientId,
            $"secret-{ClinicSetup.Suffix()}.pdf",
            "private"
        );
        var anonymous = fixture.CreateApiClient();

        // Act
        var response = await anonymous.GetAsync($"/files/{fileId}/content");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Update_Description_GetContainsDescription()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var patientId = await CreatePatientAsync(client);
        var fileId = await UploadAsync(client, patientId, $"note-{suffix}.pdf", "original note");
        var description = $"Updated note {suffix}";

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/files/{fileId}",
            new { description },
            ApiJson.Options
        );
        var fetched = await client.GetAsync($"/files/{fileId}");
        var body = await fetched.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(fetched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(description);
    }

    [Test]
    public async Task List_DeletedAndAll_ContainSoftDeletedFile()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        var fileName = $"deleted-{ClinicSetup.Suffix()}.pdf";
        var fileId = await UploadAsync(client, patientId, fileName, "to delete");
        var deleted = await client.DeleteAsync($"/files/{fileId}");
        deleted.EnsureSuccessStatusCode();

        // Act
        var onlyDeleted = await client.GetAsync($"/files?patientId={patientId}&deleted=true");
        var deletedBody = await onlyDeleted.Content.ReadAsStringAsync();
        var all = await client.GetAsync($"/files?patientId={patientId}&deleted=all");
        var allBody = await all.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(onlyDeleted.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(deletedBody).Contains(fileName);
        await Assert.That(all.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(allBody).Contains(fileName);
    }

    [Test]
    [Arguments("text/plain", "clinic note")]
    [Arguments("application/pdf", "")]
    public async Task Upload_DisallowedTypeOrEmpty_ReturnsBadRequest(string contentType, string body)
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(patientId.ToString()), "patientId");
        var bytes = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(body));
        bytes.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(bytes, "files", $"rejected-{ClinicSetup.Suffix()}.bin");
        content.Add(new StringContent("rejected"), "descriptions");

        // Act
        var response = await client.PostAsync("/files", content);
        var listed = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/files?patientId={patientId}",
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(listed!).IsEmpty();
    }

    private static async Task<Guid> CreatePatientAsync(HttpClient client)
    {
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, _) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        return patientId;
    }

    private static async Task<Guid> UploadAsync(
        HttpClient client,
        Guid patientId,
        string fileName,
        string description
    )
    {
        using var content = Form(patientId, fileName, description);
        var response = await client.PostAsync("/files", content);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        return ClinicSetup.ReadGuid(payload[0], "id");
    }

    private static MultipartFormDataContent Form(Guid patientId, string fileName, string description)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(patientId.ToString()), "patientId");
        content.Add(FilePart("clinic note"), "files", fileName);
        content.Add(new StringContent(description), "descriptions");
        return content;
    }

    private static ByteArrayContent FilePart(string text)
    {
        var bytes = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(text));
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return bytes;
    }
}
