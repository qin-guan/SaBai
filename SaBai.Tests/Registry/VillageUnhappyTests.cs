using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Registry;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class VillageUnhappyTests(AppFixture fixture)
{
    [Test]
    public async Task Get_HiddenVillage_ReturnsCode()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = $"V{ClinicSetup.Suffix()}";
        var id = await ClinicSetup.CreateVillageAsync(client, code);
        var hidden = await client.PatchAsJsonAsync(
            $"/village-codes/{id}",
            new { isVisible = false },
            ApiJson.Options);
        hidden.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync($"/village-codes/{id}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.GetProperty("code").GetString()).IsEqualTo(code);
        await Assert.That(body.GetProperty("isVisible").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task Create_InvalidColorHex_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = $"V{ClinicSetup.Suffix()}";

        // Act
        var response = await client.PostAsJsonAsync(
            "/village-codes",
            new { code, name = $"Village {code}", colorHex = "red" },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();
        var code = $"V{ClinicSetup.Suffix()}";

        // Act
        var response = await client.PostAsJsonAsync(
            "/village-codes",
            new { code, name = "", colorHex = "#336699" },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Get_MissingVillage_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync("/village-codes/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Patch_MissingVillage_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.PatchAsJsonAsync(
            "/village-codes/99999999",
            new { name = "Missing" },
            ApiJson.Options);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_MissingVillage_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync();

        // Act
        var response = await client.DeleteAsync("/village-codes/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task List_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.GetAsync("/village-codes");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
