using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Pharmacy;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class BrandTests(AppFixture fixture)
{
    [Test]
    public async Task Create_WithIngredientListContainsIngredientName()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var ingredientName = $"Ingredient {suffix}";
        var brandName = $"Brand {suffix}";
        var ingredientId = await CreateIngredientAsync(client, ingredientName);

        // Act
        var created = await client.PostAsJsonAsync(
            "/medication-brands",
            new { name = brandName, activeIngredientId = ingredientId, remarks = "" },
            ApiJson.Options);
        var listed = await client.GetAsync("/medication-brands/with-ingredient");
        var body = await listed.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(ingredientName);
        await Assert.That(body).Contains(brandName);
    }

    [Test]
    public async Task Create_UnknownIngredient_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var name = $"Missing brand {ClinicSetup.Suffix()}";

        // Act
        var response = await client.PostAsJsonAsync(
            "/medication-brands",
            new { name, activeIngredientId = 99999999, remarks = "" },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_WhenStockReferencesBrand_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, $"Stock ingredient {suffix}");
        var brandId = await CreateBrandAsync(client, $"Stock brand {suffix}", ingredientId);
        await CreateStockAsync(client, brandId, $"lot-{suffix}");

        // Act
        var response = await client.DeleteAsync($"/medication-brands/{brandId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);

        // Act
        var response = await client.DeleteAsync("/medication-brands/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    private static async Task<int> CreateIngredientAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new { name, unitOfMeasurement = "tablets", fallBelow = 5 },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        return ClinicSetup.ReadInt(await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
    }

    private static async Task<int> CreateBrandAsync(HttpClient client, string name, int ingredientId)
    {
        var response = await client.PostAsJsonAsync(
            "/medication-brands",
            new { name, activeIngredientId = ingredientId, remarks = "" },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        return ClinicSetup.ReadInt(await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
    }

    private static async Task CreateStockAsync(HttpClient client, int brandId, string location)
    {
        var response = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = brandId,
                quantity = 1,
                expiry = DateTimeOffset.UtcNow.AddYears(1),
                location,
                stockStatus = "active",
            },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
    }
}
