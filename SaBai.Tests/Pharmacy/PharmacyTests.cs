using System.Net.Http.Json;
using System.Text.Json;
using SaBai.API.Domain;
using SaBai.Tests.Support;

namespace SaBai.Tests.Pharmacy;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class PharmacyTests(AppFixture fixture)
{
    [Test]
    public async Task Catalog_CreateAndSplit_HidesConsumedParent()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ingredient = await CreateIngredientAsync(client, $"Ingredient {suffix}", fallBelow: 5);
        var brand = await CreateBrandAsync(client, $"Brand {suffix}", ingredient.Id);
        var expiry = DateTimeOffset.UtcNow.AddYears(1);
        var created = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = brand.Id,
                quantity = 10,
                expiry,
                location = $"parent-{suffix}",
                stockStatus = "active",
                remarks = "parent lot",
            },
            ApiJson.Options);
        var parent = await created.Content.ReadFromJsonAsync<StockDto>(ApiJson.Options);

        // Act
        var split = await client.PostAsJsonAsync(
            $"/medication-stock/{parent!.Id}/splits",
            new
            {
                splits = new object[]
                {
                    new { quantity = 6, location = $"child-a-{suffix}", stockStatus = "active", remarks = "a" },
                    new { quantity = 4, location = $"child-b-{suffix}", stockStatus = "reserved", remarks = "b" },
                },
            },
            ApiJson.Options);
        var listed = await client.GetFromJsonAsync<List<StockDto>>("/medication-stock", ApiJson.Options);
        var withBrand = await client.GetFromJsonAsync<List<StockWithBrandDto>>("/medication-stock/with-brand", ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(split.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var splitBody = await split.Content.ReadFromJsonAsync<SplitDto>(ApiJson.Options);
        await Assert.That(splitBody!.Success).IsTrue();
        await Assert.That(listed).IsNotNull();
        await Assert.That(listed.Any(x => x.Id == parent.Id)).IsFalse();
        await Assert.That(listed.Any(x => x.Location == $"child-a-{suffix}" && x.Quantity == 6)).IsTrue();
        await Assert.That(listed.Any(x => x.Location == $"child-b-{suffix}" && x.Quantity == 4)).IsTrue();
        await Assert.That(withBrand!.Any(x => x.Location == $"child-a-{suffix}" && x.BrandName == $"Brand {suffix}" && x.IngredientName == $"Ingredient {suffix}")).IsTrue();
    }

    [Test]
    public async Task Split_QuantityMismatch_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ingredient = await CreateIngredientAsync(client, $"Mismatch {suffix}", fallBelow: 2);
        var brand = await CreateBrandAsync(client, $"Mismatch brand {suffix}", ingredient.Id);
        var created = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = brand.Id,
                quantity = 10,
                expiry = DateTimeOffset.UtcNow.AddMonths(6),
                location = $"mismatch-{suffix}",
                stockStatus = "active",
            },
            ApiJson.Options);
        var parent = await created.Content.ReadFromJsonAsync<StockDto>(ApiJson.Options);

        // Act
        var split = await client.PostAsJsonAsync(
            $"/medication-stock/{parent!.Id}/splits",
            new
            {
                splits = new object[]
                {
                    new { quantity = 3, location = $"bad-a-{suffix}", stockStatus = "active" },
                    new { quantity = 3, location = $"bad-b-{suffix}", stockStatus = "active" },
                },
            },
            ApiJson.Options);
        var body = await split.Content.ReadAsStringAsync();
        var listed = await client.GetFromJsonAsync<List<StockDto>>("/medication-stock", ApiJson.Options);

        // Assert
        await Assert.That(split.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(body).Contains("Child stock quantity does not equal parent stock quantity!");
        await Assert.That(listed!.Single(x => x.Id == parent.Id).Quantity).IsEqualTo(10);
    }

    [Test]
    public async Task Order_CreateApproveCancel_ChangesStockOnlyWhenApproved()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ingredient = await CreateIngredientAsync(client, $"Order ingredient {suffix}", fallBelow: 50);
        var brand = await CreateBrandAsync(client, $"Order brand {suffix}", ingredient.Id);
        var stockResponse = await client.PostAsJsonAsync(
            "/medication-stock",
            new
            {
                medicationBrandId = brand.Id,
                quantity = 10,
                expiry = DateTimeOffset.UtcNow.AddMonths(3),
                location = $"order-{suffix}",
                stockStatus = "active",
            },
            ApiJson.Options);
        var stock = await stockResponse.Content.ReadFromJsonAsync<StockDto>(ApiJson.Options);
        var visit = await CreateVisitAsync(client, suffix);
        var consultResponse = await client.PostAsJsonAsync(
            "/consults",
            new
            {
                visitId = visit.VisitId,
                diagnoses = new[] { new { details = "cough", category = "Respiratory" } },
            },
            ApiJson.Options);
        consultResponse.EnsureSuccessStatusCode();
        var consultId = ReadInt(await consultResponse.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");

        // Act
        var created = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationBrandId = brand.Id,
                quantity = 4,
                dosageInstructions = "1 tablet daily",
                remarks = "with food",
            },
            ApiJson.Options);
        var order = await created.Content.ReadFromJsonAsync<OrderDto>(ApiJson.Options);
        var quantityAfterCreate = await StockQuantityAsync(client, stock!.Id);
        var queue = await client.GetAsync("/pharmacy-orders");
        var queueBody = await queue.Content.ReadAsStringAsync();

        var approved = await client.PatchAsJsonAsync(
            $"/orders/{order!.Id}",
            new { status = "APPROVED" },
            ApiJson.Options);
        var quantityAfterApprove = await StockQuantityAsync(client, stock.Id);

        var tooLarge = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationBrandId = brand.Id,
                quantity = 100,
                dosageInstructions = "too much",
            },
            ApiJson.Options);
        var tooLargeOrder = await tooLarge.Content.ReadFromJsonAsync<OrderDto>(ApiJson.Options);
        var overApprove = await client.PatchAsJsonAsync(
            $"/orders/{tooLargeOrder!.Id}",
            new { status = "APPROVED" },
            ApiJson.Options);
        var overBody = await overApprove.Content.ReadAsStringAsync();
        var quantityAfterOver = await StockQuantityAsync(client, stock.Id);

        var toCancel = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationBrandId = brand.Id,
                quantity = 2,
                dosageInstructions = "cancel me",
            },
            ApiJson.Options);
        var cancelOrder = await toCancel.Content.ReadFromJsonAsync<OrderDto>(ApiJson.Options);
        var cancelled = await client.PatchAsJsonAsync(
            $"/orders/{cancelOrder!.Id}",
            new { status = "CANCELLED" },
            ApiJson.Options);
        var quantityAfterCancel = await StockQuantityAsync(client, stock.Id);

        var secondApprove = await client.PatchAsJsonAsync(
            $"/orders/{order.Id}",
            new { status = "APPROVED" },
            ApiJson.Options);
        var secondBody = await secondApprove.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(quantityAfterCreate).IsEqualTo(10);
        await Assert.That(queue.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(queueBody).Contains(visit.PatientName);
        await Assert.That(queueBody).Contains($"Order brand {suffix}");
        await Assert.That(queueBody).Contains("cough");
        await Assert.That(queueBody).Contains(visit.DisplayId);
        await Assert.That(approved.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(quantityAfterApprove).IsEqualTo(6);
        await Assert.That(overApprove.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(overBody).Contains("Medicine stock cannot be negative.");
        await Assert.That(quantityAfterOver).IsEqualTo(6);
        await Assert.That(cancelled.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(quantityAfterCancel).IsEqualTo(6);
        await Assert.That(secondApprove.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(secondBody).Contains("Order is already APPROVED.");
    }

    [Test]
    public async Task MedicationHistory_WithoutBrandId_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.PharmacistEmail);

        // Act
        var response = await client.GetAsync("/medication-history");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static async Task<IngredientDto> CreateIngredientAsync(HttpClient client, string name, int fallBelow)
    {
        var response = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new { name, unitOfMeasurement = "tablet", fallBelow, remarks = "test" },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IngredientDto>(ApiJson.Options))!;
    }

    private static async Task<BrandDto> CreateBrandAsync(HttpClient client, string name, int ingredientId)
    {
        var response = await client.PostAsJsonAsync(
            "/medication-brands",
            new { name, activeIngredientId = ingredientId, remarks = "" },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BrandDto>(ApiJson.Options))!;
    }

    private static async Task<int> StockQuantityAsync(HttpClient client, int stockId)
    {
        var stocks = await client.GetFromJsonAsync<List<StockDto>>("/medication-stock", ApiJson.Options);
        return stocks!.Single(x => x.Id == stockId).Quantity;
    }

    private static async Task<VisitContext> CreateVisitAsync(HttpClient client, string suffix)
    {
        var code = $"V{suffix.ToUpperInvariant()}";
        var village = await client.PostAsJsonAsync(
            "/village-codes",
            new { code, name = $"Village {suffix}", colorHex = "#112233" },
            ApiJson.Options);
        village.EnsureSuccessStatusCode();
        var villageId = ReadInt(await village.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
        var patientName = $"Patient {suffix}";
        var patient = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name = patientName,
                gender = "male",
                dateOfBirth = new DateTimeOffset(1990, 1, 15, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "None",
                hasPoorCard = false,
                hasBS2Card = false,
                hasSabaiCard = false,
                villageCodeId = villageId,
            },
            ApiJson.Options);
        patient.EnsureSuccessStatusCode();
        var patientBody = await patient.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var patientId = ReadInt(patientBody, "id");
        var displayId = patientBody.TryGetProperty("displayId", out var display) && display.ValueKind == JsonValueKind.String
            ? display.GetString()!
            : Patient.FormatDisplayId(code, patientId);
        return new VisitContext(patientId, ReadInt(patientBody, "visitId"), patientName, displayId);
    }

    private static int ReadInt(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.TryGetInt32(out var id))
        {
            return id;
        }

        throw new InvalidOperationException($"Response did not include {name}: {element}");
    }

    private sealed record IngredientDto(int Id, string Name);

    private sealed record BrandDto(int Id, string Name);

    private sealed record StockDto(int Id, int Quantity, string Location);

    private sealed record StockWithBrandDto(int Id, string Location, string BrandName, string IngredientName);

    private sealed record SplitDto(bool Success);

    private sealed record OrderDto(int Id, OrderStatus Status, int Quantity);

    private sealed record VisitContext(int PatientId, int VisitId, string PatientName, string DisplayId);
}
