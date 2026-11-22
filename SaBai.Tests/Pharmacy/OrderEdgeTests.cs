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
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);

        // Act
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId = ClinicSetup.MissingId,
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
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
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
    public async Task Create_InsufficientStock_ReturnsConflictAndReservesNothing()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);
        await CreateStockAsync(client, brandId, quantity: 1, $"short-{suffix}");
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationBrandId = brandId,
                quantity = 5,
                dosageInstructions = "too many",
            },
            ApiJson.Options
        );
        var orderable = await OrderableAsync(client, brandId);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(orderable).IsEqualTo(1);
    }

    [Test]
    public async Task Update_DispensedLot_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);
        var stockId = await CreateStockAsync(client, brandId, quantity: 2, $"locked-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, brandId, quantity: 2);
        (
            await client.PatchAsJsonAsync(
                $"/orders/{orderId}",
                new { status = "APPROVED" },
                ApiJson.Options
            )
        ).EnsureSuccessStatusCode();

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/medication-stock/{stockId}",
            new { location = $"moved-{suffix}" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Approve_WhenAlreadyApproved_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
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
    public async Task Cancel_ReleasesReservedStock()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);
        await CreateStockAsync(client, brandId, quantity: 10, $"cancel-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, brandId, quantity: 2);
        var heldWhilePending = await OrderableAsync(client, brandId);

        // Act
        var cancelled = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "CANCELLED" },
            ApiJson.Options
        );
        var afterCancel = await OrderableAsync(client, brandId);

        // Assert
        await Assert.That(cancelled.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(heldWhilePending).IsEqualTo(8);
        await Assert.That(afterCancel).IsEqualTo(10);
    }

    [Test]
    public async Task PharmacyOrders_ShowsIngredientAndWhereToTakeIt()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var brandName = $"Queue brand {suffix}";
        var brandId = await CreateBrandAsync(client, suffix, brandName);
        var location = $"queue-shelf-{suffix}";
        await CreateStockAsync(client, brandId, quantity: 5, location);
        var consultId = await CreateConsultIdAsync(client);
        await CreateOrderAsync(client, consultId, brandId, quantity: 1);

        // Act
        var queue = await client.GetAsync("/pharmacy-orders");
        var body = await queue.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(queue.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains($"Order ingredient {suffix}");
        await Assert.That(body).Contains(brandName);
        await Assert.That(body).Contains(location);
    }

    [Test]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();

        // Act
        var response = await client.GetAsync($"/orders/{ClinicSetup.MissingId}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateApiClient();

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

    [Test]
    public async Task Create_ByIngredient_ReservesEarliestExpiryFirstAcrossBrands()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandA = await CreateBrandForAsync(client, ingredientId, $"A-{suffix}");
        var brandB = await CreateBrandForAsync(client, ingredientId, $"B-{suffix}");
        await CreateStockAsync(client, brandA, 5, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(9));
        await CreateStockAsync(client, brandB, 5, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        await CreateStockAsync(client, brandB, 5, $"middle-{suffix}", DateTimeOffset.UtcNow.AddMonths(5));
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationActiveIngredientId = ingredientId,
                quantity = 7,
                dosageInstructions = "as directed",
            },
            ApiJson.Options
        );
        var order = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var parts = order
            .GetProperty("allocations")
            .EnumerateArray()
            .Select(x => (
                Location: x.GetProperty("location").GetString(),
                Brand: x.GetProperty("brandName").GetString(),
                Quantity: x.GetProperty("quantity").GetInt32()
            ))
            .ToList();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(parts.Count).IsEqualTo(2);
        await Assert.That(parts[0]).IsEqualTo(($"early-{suffix}", $"B-{suffix}", 5));
        await Assert.That(parts[1]).IsEqualTo(($"middle-{suffix}", $"B-{suffix}", 2));
        await Assert.That(order.GetProperty("medicationBrandId").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Create_WithBrand_OnlyUsesThatBrandsStock()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandA = await CreateBrandForAsync(client, ingredientId, $"A-{suffix}");
        var brandB = await CreateBrandForAsync(client, ingredientId, $"B-{suffix}");
        await CreateStockAsync(client, brandA, 5, $"a-{suffix}", DateTimeOffset.UtcNow.AddMonths(8));
        await CreateStockAsync(client, brandB, 5, $"b-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationBrandId = brandA,
                quantity = 3,
                dosageInstructions = "as directed",
            },
            ApiJson.Options
        );
        var order = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var tooMuch = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationBrandId = brandA,
                quantity = 3,
                dosageInstructions = "brand A only has 2 left",
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(ClinicSetup.ReadGuid(order, "medicationBrandId")).IsEqualTo(brandA);
        await Assert.That(order.GetProperty("allocations")[0].GetProperty("location").GetString()).IsEqualTo($"a-{suffix}");
        await Assert.That(tooMuch.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Create_TwoOrdersForTheSameStock_FirstOneWins()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"race-{suffix}");
        await CreateStockAsync(client, brandId, 5, $"race-{suffix}", DateTimeOffset.UtcNow.AddMonths(4));
        var consultId = await CreateConsultIdAsync(client);

        Task<HttpResponseMessage> Place() =>
            client.PostAsJsonAsync(
                "/orders",
                new
                {
                    consultId,
                    medicationActiveIngredientId = ingredientId,
                    quantity = 3,
                    dosageInstructions = "race",
                },
                ApiJson.Options
            );

        // Act: both ask at once for 3 of the 5 units, so only one can be covered.
        var results = await Task.WhenAll(Place(), Place());

        // Assert
        var codes = results.Select(x => x.StatusCode).OrderBy(x => (int)x).ToList();
        await Assert.That(codes).IsEquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        await Assert.That(await OrderableAsync(client, brandId)).IsEqualTo(2);
    }

    [Test]
    public async Task Delete_PendingOrder_ReleasesItsStock()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var brandId = await CreateBrandAsync(client, suffix);
        await CreateStockAsync(client, brandId, quantity: 6, $"delete-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, brandId, quantity: 4);
        var held = await OrderableAsync(client, brandId);

        // Act
        var deleted = await client.DeleteAsync($"/orders/{orderId}");
        var afterDelete = await OrderableAsync(client, brandId);

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(held).IsEqualTo(2);
        await Assert.That(afterDelete).IsEqualTo(6);
    }

    [Test]
    public async Task Consult_WithOrders_ReservesStockAndRefusesAnUncoveredOrder()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"consult-{suffix}");
        await CreateStockAsync(client, brandId, 5, $"consult-{suffix}", DateTimeOffset.UtcNow.AddMonths(3));
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, okVisit) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        var (_, badVisit) = await ClinicSetup.RegisterPatientAsync(client, villageId);

        // Act
        var covered = await client.PostAsJsonAsync(
            "/consults",
            new
            {
                visitId = okVisit,
                consultation = "exam",
                diagnoses = new[] { new { details = "cough", category = "Respiratory" } },
                orders = new[]
                {
                    new
                    {
                        medicationActiveIngredientId = ingredientId,
                        quantity = 4,
                        dosageInstructions = "1 daily",
                    },
                },
            },
            ApiJson.Options
        );
        var afterCovered = await OrderableAsync(client, brandId);
        var uncovered = await client.PostAsJsonAsync(
            "/consults",
            new
            {
                visitId = badVisit,
                consultation = "exam",
                diagnoses = new[] { new { details = "cough", category = "Respiratory" } },
                orders = new[]
                {
                    new
                    {
                        medicationActiveIngredientId = ingredientId,
                        quantity = 4,
                        dosageInstructions = "1 daily",
                    },
                },
            },
            ApiJson.Options
        );
        var consultsForBadVisit = await client.GetAsync($"/consults?visitId={badVisit}");

        // Assert
        await Assert.That(covered.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(afterCovered).IsEqualTo(1);
        await Assert.That(uncovered.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await OrderableAsync(client, brandId)).IsEqualTo(1);
        await Assert.That((await consultsForBadVisit.Content.ReadAsStringAsync())).DoesNotContain(badVisit.ToString());
    }

    private static async Task<Guid> CreateIngredientAsync(HttpClient client, string suffix)
    {
        var ingredient = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new
            {
                name = $"Order ingredient {suffix}",
                unitOfMeasurement = "tablets",
                fallBelow = 1,
            },
            ApiJson.Options
        );
        ingredient.EnsureSuccessStatusCode();
        return ClinicSetup.ReadGuid(
            await ingredient.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static async Task<Guid> CreateBrandForAsync(
        HttpClient client,
        Guid ingredientId,
        string name
    )
    {
        var brand = await client.PostAsJsonAsync(
            "/medication-brands",
            new
            {
                name,
                activeIngredientId = ingredientId,
                remarks = "",
            },
            ApiJson.Options
        );
        brand.EnsureSuccessStatusCode();
        return ClinicSetup.ReadGuid(
            await brand.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    /// <summary>Active, unexpired quantity of one brand that can still be ordered.</summary>
    private static async Task<int> OrderableAsync(HttpClient client, Guid brandId)
    {
        var lots = await client.GetFromJsonAsync<List<JsonElement>>(
            "/medication-stock/with-brand",
            ApiJson.Options
        );
        return lots!
            .Where(x =>
                ClinicSetup.ReadGuid(x, "medicationBrandId") == brandId
                && x.GetProperty("stockStatus").GetString() == "active"
            )
            .Sum(x => x.GetProperty("quantity").GetInt32());
    }

    private static async Task<Guid> CreateBrandAsync(
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
        var ingredientId = ClinicSetup.ReadGuid(
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
        return ClinicSetup.ReadGuid(
            await brand.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static async Task<Guid> CreateStockAsync(
        HttpClient client,
        Guid brandId,
        int quantity,
        string location,
        DateTimeOffset? expiry = null
    )
    {
        var response = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = brandId,
                quantity,
                expiry = expiry ?? DateTimeOffset.UtcNow.AddYears(1),
                location,
                stockStatus = "active",
            },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return ClinicSetup.ReadGuid(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static async Task<Guid> CreateConsultIdAsync(HttpClient client)
    {
        var villageId = await ClinicSetup.CreateVillageAsync(client);
        var (_, visitId) = await ClinicSetup.RegisterPatientAsync(client, villageId);
        return await ClinicSetup.CreateConsultAsync(client, visitId);
    }

    private static async Task<Guid> CreateOrderAsync(
        HttpClient client,
        Guid consultId,
        Guid brandId,
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
        return ClinicSetup.ReadGuid(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    private static StockLot FindLot(string body, Guid stockId)
    {
        var stocks =
            JsonSerializer.Deserialize<List<StockLot>>(body, ApiJson.Options)
            ?? throw new InvalidOperationException($"Stock list was not JSON: {body}");
        return stocks.SingleOrDefault(x => x.Id == stockId)
            ?? throw new InvalidOperationException($"Stock {stockId} was not listed: {body}");
    }

    private sealed record StockLot(Guid Id, int Quantity, string Location);
}
