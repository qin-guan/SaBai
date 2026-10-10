using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Pharmacy;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class IngredientTests(AppFixture fixture)
{
    [Test]
    public async Task Create_ListAndUpdate_PersistsName()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var name = $"Ingredient {suffix}";
        var updatedName = $"Revised {suffix}";

        // Act
        var created = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new { name, unitOfMeasurement = "tablets", fallBelow = 5 },
            ApiJson.Options);
        var id = ClinicSetup.ReadInt(await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
        var listed = await client.GetAsync("/medication-ingredients");
        var listedBody = await listed.Content.ReadAsStringAsync();
        var updated = await client.PutAsJsonAsync(
            $"/medication-ingredients/{id}",
            new { name = updatedName, unitOfMeasurement = "tablets", fallBelow = 5 },
            ApiJson.Options);
        var after = await client.GetAsync("/medication-ingredients");
        var afterBody = await after.Content.ReadAsStringAsync();
        var saved = Find(afterBody, id);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(listedBody).Contains(name);
        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(afterBody).Contains(updatedName);
        await Assert.That(saved.Name).IsEqualTo(updatedName);
    }

    [Test]
    public async Task Create_FallBelowZero_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var name = $"Zero {ClinicSetup.Suffix()}";

        // Act
        var response = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new { name, unitOfMeasurement = "tablets", fallBelow = 0 },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var name = $"Missing {ClinicSetup.Suffix()}";

        // Act
        var response = await client.PutAsJsonAsync(
            "/medication-ingredients/99999999",
            new { name, unitOfMeasurement = "tablets", fallBelow = 5 },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_WhenBrandReferencesIngredient_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, $"Linked {suffix}");
        await CreateBrandAsync(client, $"Linked brand {suffix}", ingredientId);

        // Act
        var response = await client.DeleteAsync($"/medication-ingredients/{ingredientId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Delete_WithoutBrands_ReturnsNoContent()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var name = $"Unused {ClinicSetup.Suffix()}";
        var ingredientId = await CreateIngredientAsync(client, name);

        // Act
        var deleted = await client.DeleteAsync($"/medication-ingredients/{ingredientId}");
        var listed = await client.GetAsync("/medication-ingredients");
        var body = await listed.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.Contains(name)).IsFalse();
    }

    [Test]
    public async Task List_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.GetAsync("/medication-ingredients");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
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

    private static async Task CreateBrandAsync(HttpClient client, string name, int ingredientId)
    {
        var response = await client.PostAsJsonAsync(
            "/medication-brands",
            new { name, activeIngredientId = ingredientId, remarks = "" },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
    }

    private static IngredientItem Find(string body, int id)
    {
        var ingredients = JsonSerializer.Deserialize<List<IngredientItem>>(body, ApiJson.Options)
            ?? throw new InvalidOperationException($"Ingredient list was not JSON: {body}");
        return ingredients.SingleOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Ingredient {id} was not listed: {body}");
    }

    private sealed record IngredientItem(int Id, string Name);
}
