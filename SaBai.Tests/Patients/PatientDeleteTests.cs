using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Patients;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class PatientDeleteTests(AppFixture fixture)
{
    [Test]
    public async Task Delete_AsAdmin_RemovesPatientAndAllRecords()
    {
        // Arrange
        var client = await fixture.CreateApiClient().LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var village = await ClinicSetup.CreateVillageAsync(client);
        var (patientId, visitId) = await ClinicSetup.RegisterPatientAsync(client, village);
        var consultId = await ClinicSetup.CreateConsultAsync(client, visitId);
        var stockId = await CreateStockAsync(client, suffix, quantity: 10);
        var brandId = await BrandOfStockAsync(client, stockId);
        var approvedOrder = await CreateOrderAsync(client, consultId, brandId, quantity: 3);
        (
            await client.PatchAsJsonAsync(
                $"/orders/{approvedOrder}",
                new { status = "APPROVED" },
                ApiJson.Options
            )
        ).EnsureSuccessStatusCode();
        await CreateOrderAsync(client, consultId, brandId, quantity: 2);
        var fileId = await UploadFileAsync(client, patientId, suffix);

        // Act
        var deleted = await client.DeleteAsync($"/patients/{patientId}");
        var patient = await client.GetAsync($"/patients/{patientId}");
        var visit = await client.GetAsync($"/visits/{visitId}");
        var consult = await client.GetAsync($"/consults/{consultId}");
        var file = await client.GetAsync($"/files/{fileId}");
        var quantity = await StockQuantityAsync(client, stockId);
        var deletedAgain = await client.DeleteAsync($"/patients/{patientId}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(patient.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(visit.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(consult.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(quantity).IsEqualTo(7);
        await Assert.That(deletedAgain.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_AsMember_ReturnsForbidden()
    {
        // Arrange
        var admin = await fixture.CreateApiClient().LoginAsync();
        var village = await ClinicSetup.CreateVillageAsync(admin);
        var (patientId, _) = await ClinicSetup.RegisterPatientAsync(admin, village);
        var member = await fixture.CreateApiClient().LoginAsync(ClinicApi.DoctorEmail);

        // Act
        var deleted = await member.DeleteAsync($"/patients/{patientId}");
        var patient = await admin.GetAsync($"/patients/{patientId}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(patient.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Delete_MissingPatient_ReturnsNotFound()
    {
        // Arrange
        var client = await fixture.CreateApiClient().LoginAsync();

        // Act
        var response = await client.DeleteAsync("/patients/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static async Task<int> CreateStockAsync(HttpClient client, string suffix, int quantity)
    {
        var ingredient = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new
            {
                name = $"Delete ingredient {suffix}",
                unitOfMeasurement = "tablet",
                fallBelow = 1,
                remarks = "",
            },
            ApiJson.Options
        );
        ingredient.EnsureSuccessStatusCode();
        var brand = await client.PostAsJsonAsync(
            "/medication-brands",
            new
            {
                name = $"Delete brand {suffix}",
                activeIngredientId = await ReadIdAsync(ingredient),
                remarks = "",
            },
            ApiJson.Options
        );
        brand.EnsureSuccessStatusCode();
        var stock = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = await ReadIdAsync(brand),
                quantity,
                expiry = DateTimeOffset.UtcNow.AddMonths(3),
                location = $"delete-{suffix}",
                stockStatus = "active",
            },
            ApiJson.Options
        );
        stock.EnsureSuccessStatusCode();
        return await ReadIdAsync(stock);
    }

    private static async Task<int> BrandOfStockAsync(HttpClient client, int stockId)
    {
        var stocks = await client.GetFromJsonAsync<List<JsonElement>>(
            "/medication-stock",
            ApiJson.Options
        );
        return ClinicSetup.ReadInt(
            stocks!.Single(x => ClinicSetup.ReadInt(x, "id") == stockId),
            "medicationBrandId"
        );
    }

    private static async Task<int> StockQuantityAsync(HttpClient client, int stockId)
    {
        var stocks = await client.GetFromJsonAsync<List<JsonElement>>(
            "/medication-stock",
            ApiJson.Options
        );
        return ClinicSetup.ReadInt(
            stocks!.Single(x => ClinicSetup.ReadInt(x, "id") == stockId),
            "quantity"
        );
    }

    private static async Task<int> CreateOrderAsync(
        HttpClient client,
        int consultId,
        int brandId,
        int quantity
    )
    {
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationBrandId = brandId,
                quantity,
                dosageInstructions = "1 tablet daily",
            },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return await ReadIdAsync(response);
    }

    private static async Task<int> UploadFileAsync(HttpClient client, int patientId, string suffix)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(patientId.ToString()), "patientId");
        var bytes = new ByteArrayContent([.. "clinic note"u8]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(bytes, "files", $"note-{suffix}.pdf");
        content.Add(new StringContent("note"), "descriptions");
        var response = await client.PostAsync("/files", content);
        response.EnsureSuccessStatusCode();
        var files = await response.Content.ReadFromJsonAsync<List<JsonElement>>(ApiJson.Options);
        return ClinicSetup.ReadInt(files![0], "id");
    }

    private static async Task<int> ReadIdAsync(HttpResponseMessage response) =>
        ClinicSetup.ReadInt(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
}
