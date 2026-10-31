using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Pharmacy;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class StockTests(AppFixture fixture)
{
    [Test]
    public async Task Adjust_Increase_ListShowsNewQuantity()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var location = $"adjust-{suffix}";
        var stockId = await CreateStockAsync(client, suffix, quantity: 5, location);

        // Act
        var adjusted = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/adjustments",
            new { quantityChange = 2 },
            ApiJson.Options
        );
        var listed = await client.GetAsync("/medication-stock");
        var body = await listed.Content.ReadAsStringAsync();
        var lot = FindLot(body, stockId);

        // Assert
        await Assert.That(adjusted.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(location);
        await Assert.That(lot.Quantity).IsEqualTo(7);
    }

    [Test]
    public async Task Adjust_BelowZero_ReturnsBadRequestAndLeavesQuantity()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var location = $"floor-{suffix}";
        var stockId = await CreateStockAsync(client, suffix, quantity: 3, location);

        // Act
        var adjusted = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/adjustments",
            new { quantityChange = -4 },
            ApiJson.Options
        );
        var listed = await client.GetAsync("/medication-stock");
        var body = await listed.Content.ReadAsStringAsync();
        var lot = FindLot(body, stockId);

        // Assert
        await Assert.That(adjusted.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(location);
        await Assert.That(lot.Quantity).IsEqualTo(3);
    }

    [Test]
    public async Task Create_UnknownBrand_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var location = $"missing-{ClinicSetup.Suffix()}";

        // Act
        var response = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = 99999999,
                quantity = 1,
                expiry = DateTimeOffset.UtcNow.AddMonths(1),
                location,
                stockStatus = "active",
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);

        // Act
        var response = await client.PatchAsJsonAsync(
            "/medication-stock/99999999",
            new { location = $"missing-{ClinicSetup.Suffix()}" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Split_SingleChild_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var stockId = await CreateStockAsync(client, suffix, quantity: 4, $"parent-{suffix}");

        // Act
        var response = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/splits",
            new
            {
                splits = new object[]
                {
                    new
                    {
                        quantity = 1,
                        location = $"only-{suffix}",
                        stockStatus = "active",
                    },
                },
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Update_Location_ListContainsNewLocation()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var stockId = await CreateStockAsync(client, suffix, quantity: 2, $"shelf-{suffix}");
        var updatedLocation = $"moved-{suffix}";

        // Act
        var patched = await client.PatchAsJsonAsync(
            $"/medication-stock/{stockId}",
            new { location = updatedLocation },
            ApiJson.Options
        );
        var listed = await client.GetAsync("/medication-stock");
        var body = await listed.Content.ReadAsStringAsync();
        var lot = FindLot(body, stockId);

        // Assert
        await Assert.That(patched.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(updatedLocation);
        await Assert.That(lot.Location).IsEqualTo(updatedLocation);
    }

    [Test]
    public async Task CreateAndSplit_RecordQuantityHistory()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var (stockId, brandId) = await CreateStockWithBrandAsync(
            client,
            suffix,
            quantity: 6,
            $"history-{suffix}"
        );

        // Act
        var split = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/splits",
            new
            {
                splits = new object[]
                {
                    new
                    {
                        quantity = 4,
                        location = $"left-{suffix}",
                        stockStatus = "active",
                    },
                    new
                    {
                        quantity = 2,
                        location = $"right-{suffix}",
                        stockStatus = "active",
                    },
                },
            },
            ApiJson.Options
        );
        var history = await client.GetFromJsonAsync<List<HistoryRow>>(
            $"/medication-history?brandId={brandId}",
            ApiJson.Options
        );

        // Assert
        await Assert.That(split.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert
            .That(history!.Where(x => x.Kind == "adjustment").Select(x => x.QuantityChanged).Order())
            .IsEquivalentTo(new[] { -6, 2, 4, 6 });
    }

    [Test]
    public async Task WithBrand_FlagsLotsAtOrBelowFallBelow()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var (lowId, _) = await CreateStockWithBrandAsync(
            client,
            $"low-{suffix}",
            quantity: 5,
            $"low-{suffix}"
        );
        var (okId, _) = await CreateStockWithBrandAsync(
            client,
            $"ok-{suffix}",
            quantity: 6,
            $"ok-{suffix}"
        );

        // Act
        var listed = await client.GetFromJsonAsync<List<LowStockRow>>(
            "/medication-stock/with-brand",
            ApiJson.Options
        );

        // Assert
        await Assert.That(listed!.Single(x => x.Id == lowId).IsLowStock).IsTrue();
        await Assert.That(listed!.Single(x => x.Id == okId).IsLowStock).IsFalse();
    }

    private static async Task<int> CreateStockAsync(
        HttpClient client,
        string suffix,
        int quantity,
        string location
    ) => (await CreateStockWithBrandAsync(client, suffix, quantity, location)).StockId;

    private static async Task<(int StockId, int BrandId)> CreateStockWithBrandAsync(
        HttpClient client,
        string suffix,
        int quantity,
        string location
    )
    {
        var ingredient = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new
            {
                name = $"Stock ingredient {suffix}",
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
                name = $"Stock brand {suffix}",
                activeIngredientId = ingredientId,
                remarks = "",
            },
            ApiJson.Options
        );
        brand.EnsureSuccessStatusCode();
        var brandId = ClinicSetup.ReadInt(
            await brand.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
        var stock = await client.PostAsJsonAsync(
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
        stock.EnsureSuccessStatusCode();
        var stockId = ClinicSetup.ReadInt(
            await stock.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
        return (stockId, brandId);
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

    private sealed record HistoryRow(string Kind, int QuantityChanged);

    private sealed record LowStockRow(int Id, bool IsLowStock);
}
