using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Pharmacy;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class OrderEdgeTests(AppFixture fixture)
{
    [Test]
    public async Task Create_UnknownConsult_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);

        // Act
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId = 99999999,
                medicationBrandId = brandId,
                quantity = 1,
                dosageInstructions = "1 tablet daily",
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_QuantityZero_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationBrandId = brandId,
                quantity = 0,
                dosageInstructions = "1 tablet daily",
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Approve_InsufficientStock_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);
        await CreateStockAsync(client, brandId, quantity: 1, $"short-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, brandId, quantity: 5);

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "APPROVED" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Approve_WhenAlreadyApproved_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);
        await CreateStockAsync(client, brandId, quantity: 10, $"again-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, brandId, quantity: 2);
        var approved = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "APPROVED" },
            ApiJson.Options
        );
        approved.EnsureSuccessStatusCode();

        // Act
        var second = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "APPROVED" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Cancel_LeavesStockQuantityUnchanged()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);
        var location = $"cancel-{suffix}";
        var stockId = await CreateStockAsync(client, brandId, quantity: 10, location);
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, brandId, quantity: 2);

        // Act
        var cancelled = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "CANCELLED" },
            ApiJson.Options
        );
        var listed = await client.GetAsync("/medication-stock");
        var body = await listed.Content.ReadAsStringAsync();
        var lot = FindLot(body, stockId);

        // Assert
        await Assert.That(cancelled.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(location);
        await Assert.That(lot.Quantity).IsEqualTo(10);
    }

    [Test]
    public async Task PharmacyOrders_ContainsMedicationName()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var medicationName = $"Queue med {suffix}";
        var brandId = await CreateBrandAsync(client, suffix, medicationName);
        var consultId = await CreateConsultIdAsync(client);
        await CreateOrderAsync(client, consultId, brandId, quantity: 1);

        // Act
        var queue = await client.GetAsync("/pharmacy-orders");
        var body = await queue.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(queue.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(medicationName);
    }

    [Test]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);

        // Act
        var response = await client.GetAsync("/orders/99999999");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");

        // Act
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId = 1,
                medicationBrandId = 1,
                quantity = 1,
                dosageInstructions = "1 tablet daily",
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private static async Task<int> CreateBrandAsync(
        HttpClient client,
        string suffix,
        string? name = null
    )
    {
        var ingredient = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new
            {
                name = $"Order ingredient {suffix}",
                unitOfMeasurement = "tablets",
                fallBelow = 5,
            },
            ApiJson.Options
        );
        ingredient.EnsureSuccessStatusCode();
        var ingredientId = ClinicSetup.ReadInt(
            await ingredient.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
        var brand = await client.PostAsJsonAsync(
            "/medication-brands",
            new
            {
                name = name ?? $"Order brand {suffix}",
                activeIngredientId = ingredientId,
                remarks = "",
            },
            ApiJson.Options
        );
        brand.EnsureSuccessStatusCode();
        return ClinicSetup.ReadInt(
            await brand.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static async Task<int> CreateStockAsync(
        HttpClient client,
        int brandId,
        int quantity,
        string location
    )
    {
        var response = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = brandId,
                quantity,
                expiry = DateTimeOffset.UtcNow.AddYears(1),
                location,
                stockStatus = "active",
            },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return ClinicSetup.ReadInt(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static async Task<int> CreateConsultIdAsync(HttpClient client)
    {
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        return await ClinicSetup.CreateConsultAsync(client, visitId);
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
        return ClinicSetup.ReadInt(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static StockLot FindLot(string body, int stockId)
    {
        var stocks =
            JsonSerializer.Deserialize<List<StockLot>>(body, ApiJson.Options)
            ?? throw new InvalidOperationException($"Stock list was not JSON: {body}");
        return stocks.SingleOrDefault(x => x.Id == stockId)
            ?? throw new InvalidOperationException($"Stock {stockId} was not listed: {body}");
    }

    private sealed record StockLot(int Id, int Quantity, string Location);
}
