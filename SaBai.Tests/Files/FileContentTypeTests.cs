using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Files;

/// <summary>Uploads are typed by their bytes, not the client's Content-Type, and served so they cannot run in the app.</summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class FileContentTypeTests(AppFixture fixture)
{
    [Test]
    [Arguments("image/png", ".png")]
    [Arguments("application/pdf", ".pdf")]
    [Arguments("image/jpeg", ".jpg")]
    public async Task Upload_HtmlClaimingAllowedType_ReturnsBadRequest(
        string claimedType,
        string extension
    )
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        using var content = Form(
            patientId,
            SampleFiles.Part(SampleFiles.Html, claimedType),
            $"script-{ClinicSetup.Suffix()}{extension}"
        );

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

    [Test]
    public async Task Upload_ZipThatIsNotAWordDocument_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        using var content = Form(
            patientId,
            SampleFiles.Part(SampleFiles.Zip(), SampleFiles.DocxType),
            $"archive-{ClinicSetup.Suffix()}.docx"
        );

        // Act
        var response = await client.PostAsync("/files", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Upload_MacroEnabledWordDocument_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        using var content = Form(
            patientId,
            SampleFiles.Part(SampleFiles.Docx(macroEnabled: true), SampleFiles.DocxType),
            $"macro-{ClinicSetup.Suffix()}.docx"
        );

        // Act
        var response = await client.PostAsync("/files", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Upload_ExtensionNotMatchingContent_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        using var content = Form(
            patientId,
            SampleFiles.Part(SampleFiles.Pdf, "application/pdf"),
            $"report-{ClinicSetup.Suffix()}.png"
        );

        // Act
        var response = await client.PostAsync("/files", content);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("pdf")]
    [Arguments("png")]
    [Arguments("jpeg")]
    [Arguments("docx")]
    public async Task Upload_GenuineFile_IsServedWithTypeFromItsContent(string kind)
    {
        // Arrange
        var (bytes, type, extension) = kind switch
        {
            "pdf" => (SampleFiles.Pdf, "application/pdf", ".pdf"),
            "png" => (SampleFiles.Png(), "image/png", ".png"),
            "jpeg" => (SampleFiles.Jpeg(), "image/jpeg", ".jpg"),
            _ => (SampleFiles.Docx(), SampleFiles.DocxType, ".docx"),
        };
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        // The client's header is wrong on purpose: only the bytes decide the stored type.
        using var content = Form(
            patientId,
            SampleFiles.Part(bytes, "text/html"),
            $"upload-{ClinicSetup.Suffix()}{extension}"
        );

        // Act
        var uploaded = await client.PostAsync("/files", content);
        var created = await uploaded.Content.ReadFromJsonAsync<List<JsonElement>>(
            ApiJson.Options
        );
        var fileId = created![0].GetProperty("id").GetGuid();
        var storedName = created[0].GetProperty("storedFileName").GetString();
        var downloaded = await client.GetAsync($"/files/{fileId}/content");
        var downloadedBytes = await downloaded.Content.ReadAsByteArrayAsync();

        // Assert
        await Assert.That(uploaded.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(storedName!.EndsWith(extension, StringComparison.Ordinal)).IsTrue();
        await Assert.That(downloaded.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(downloaded.Content.Headers.ContentType?.MediaType).IsEqualTo(type);
        await Assert.That(downloadedBytes.SequenceEqual(bytes)).IsTrue();
    }

    [Test]
    public async Task Download_IsAttachmentWithNoSniffAndSandboxPolicy()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var patientId = await CreatePatientAsync(client);
        using var content = Form(
            patientId,
            SampleFiles.Part(SampleFiles.Pdf, "application/pdf"),
            "ผลตรวจ lab.pdf"
        );
        var uploaded = await client.PostAsync("/files", content);
        uploaded.EnsureSuccessStatusCode();
        var created = await uploaded.Content.ReadFromJsonAsync<List<JsonElement>>(
            ApiJson.Options
        );
        var fileId = created![0].GetProperty("id").GetGuid();

        // Act
        var response = await client.GetAsync($"/files/{fileId}/content");
        var disposition = response.Content.Headers.ContentDisposition;

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert
            .That(string.Join(",", response.Headers.GetValues("X-Content-Type-Options")))
            .IsEqualTo("nosniff");
        await Assert
            .That(string.Join(",", response.Headers.GetValues("Content-Security-Policy")))
            .Contains("sandbox");
        await Assert.That(disposition?.DispositionType).IsEqualTo("attachment");
        await Assert.That(disposition?.FileNameStar).IsEqualTo("ผลตรวจ lab.pdf");
    }

    [Test]
    public async Task JsonResponses_HaveNoSniff()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);

        // Act
        var ok = await client.GetAsync("/village-codes");
        var notFound = await client.GetAsync($"/files/{ClinicSetup.MissingId}");

        // Assert
        await Assert.That(ok.Headers.Contains("X-Content-Type-Options")).IsTrue();
        await Assert.That(notFound.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(notFound.Headers.Contains("X-Content-Type-Options")).IsTrue();
    }

    private static async Task<Guid> CreatePatientAsync(HttpClient client)
    {
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, _) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        return patientId;
    }

    private static MultipartFormDataContent Form(Guid patientId, HttpContent file, string fileName)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(patientId.ToString()), "patientId");
        content.Add(file, "files", fileName);
        content.Add(new StringContent("upload"), "descriptions");
        return content;
    }
}
