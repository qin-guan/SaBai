using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Pharmacy;

/// <summary>
/// The pharmacist's own choice of stock batches for a pending order (replacing the automatic
/// earliest-expiry allocation), the list of batches to choose from, and a batch's change log
/// with what was dispensed from it and to whom.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class OrderAllocationTests(AppFixture fixture)
{
    [Test]
    public async Task AllocationOptions_ListsBatchesOfTheIngredientWithWhatTheOrderMayTake()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix, "sachet");
        var otherIngredientId = await CreateIngredientAsync(client, $"other-{suffix}");
        var brandA = await CreateBrandForAsync(client, ingredientId, $"A-{suffix}");
        var brandB = await CreateBrandForAsync(client, ingredientId, $"B-{suffix}");
        var otherBrand = await CreateBrandForAsync(client, otherIngredientId, $"O-{suffix}");
        var early = await CreateStockAsync(client, brandA, 10, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var late = await CreateStockAsync(client, brandB, 8, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(8));
        await CreateStockAsync(client, brandA, 5, $"expired-{suffix}", DateTimeOffset.UtcNow.AddDays(-1));
        await CreateStockAsync(client, brandA, 5, $"discarded-{suffix}", status: "discarded");
        await CreateStockAsync(client, otherBrand, 5, $"other-{suffix}");
        var consultId = await CreateConsultIdAsync(admin);
        var orderId = await CreateOrderAsync(admin, consultId, ingredientId, 4);
        await CreateOrderAsync(admin, consultId, ingredientId, 3);

        // Act
        var options = await client.GetFromJsonAsync<JsonElement>(
            $"/orders/{orderId}/allocation-options",
            ApiJson.Options
        );
        var batches = options.GetProperty("batches").EnumerateArray().ToList();

        // Assert
        await Assert.That(options.GetProperty("unitOfMeasurement").GetString()).IsEqualTo("sachet");
        await Assert.That(options.GetProperty("quantity").GetInt32()).IsEqualTo(4);
        await Assert.That(batches.Count).IsEqualTo(2);
        await Assert.That(ClinicSetup.ReadGuid(batches[0], "stockId")).IsEqualTo(early);
        await Assert.That(batches[0].GetProperty("brandName").GetString()).IsEqualTo($"A-{suffix}");
        await Assert.That(batches[0].GetProperty("location").GetString()).IsEqualTo($"early-{suffix}");
        await Assert.That(batches[0].GetProperty("quantity").GetInt32()).IsEqualTo(10);
        await Assert.That(batches[0].GetProperty("reservedQuantity").GetInt32()).IsEqualTo(7);
        await Assert.That(batches[0].GetProperty("availableQuantity").GetInt32()).IsEqualTo(3);
        await Assert.That(batches[0].GetProperty("allocatedQuantity").GetInt32()).IsEqualTo(4);
        // The other order's 3 units are not this order's to take.
        await Assert.That(batches[0].GetProperty("maxQuantity").GetInt32()).IsEqualTo(7);
        await Assert.That(ClinicSetup.ReadGuid(batches[1], "stockId")).IsEqualTo(late);
        await Assert.That(batches[1].GetProperty("allocatedQuantity").GetInt32()).IsEqualTo(0);
        await Assert.That(batches[1].GetProperty("maxQuantity").GetInt32()).IsEqualTo(8);
    }

    [Test]
    public async Task ReplaceAllocations_MovesTheReservationsToTheChosenBatches()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandA = await CreateBrandForAsync(client, ingredientId, $"A-{suffix}");
        var brandB = await CreateBrandForAsync(client, ingredientId, $"B-{suffix}");
        var early = await CreateStockAsync(client, brandA, 10, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var middle = await CreateStockAsync(client, brandB, 10, $"middle-{suffix}", DateTimeOffset.UtcNow.AddMonths(5));
        var late = await CreateStockAsync(client, brandA, 10, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(9));
        var consultId = await CreateConsultIdAsync(admin);
        var orderId = await CreateOrderAsync(admin, consultId, ingredientId, 6);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/orders/{orderId}/allocations",
            new
            {
                allocations = new[]
                {
                    new { stockId = late, quantity = 4 },
                    new { stockId = middle, quantity = 2 },
                },
            },
            ApiJson.Options
        );
        var order = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var parts = Allocations(order);
        var again = await client.PutAsJsonAsync(
            $"/orders/{orderId}/allocations",
            new
            {
                allocations = new[]
                {
                    new { stockId = middle, quantity = 2 },
                    new { stockId = late, quantity = 4 },
                },
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(order.GetProperty("status").GetString()).IsEqualTo("PENDING");
        await Assert.That(order.GetProperty("unitOfMeasurement").GetString()).IsEqualTo("tablets");
        await Assert.That(parts.Count).IsEqualTo(2);
        await Assert.That(parts[0]).IsEqualTo(($"middle-{suffix}", $"B-{suffix}", 2));
        await Assert.That(parts[1]).IsEqualTo(($"late-{suffix}", $"A-{suffix}", 4));
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await BatchAsync(client, early)).ReservedQuantity).IsEqualTo(0);
        await Assert.That((await BatchAsync(client, middle)).ReservedQuantity).IsEqualTo(2);
        await Assert.That((await BatchAsync(client, late)).ReservedQuantity).IsEqualTo(4);
        await Assert.That((await BatchAsync(client, late)).Quantity).IsEqualTo(10);
    }

    [Test]
    public async Task ReplaceAllocations_CountsTheOrdersOwnUnitsAsAvailable()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"own-{suffix}");
        var early = await CreateStockAsync(client, brandId, 5, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var late = await CreateStockAsync(client, brandId, 5, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(9));
        var consultId = await CreateConsultIdAsync(admin);
        // Holds all 5 of the early batch and 2 of the late one.
        var orderId = await CreateOrderAsync(admin, consultId, ingredientId, 7);

        // Act
        var response = await client.PutAsJsonAsync(
            $"/orders/{orderId}/allocations",
            new
            {
                allocations = new[]
                {
                    new { stockId = early, quantity = 2 },
                    new { stockId = late, quantity = 5 },
                },
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await BatchAsync(client, early)).ReservedQuantity).IsEqualTo(2);
        await Assert.That((await BatchAsync(client, late)).ReservedQuantity).IsEqualTo(5);
    }

    [Test]
    public async Task ReplaceAllocations_InvalidChoices_AreRefusedWithAMessageAndChangeNothing()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var otherIngredientId = await CreateIngredientAsync(client, $"other-{suffix}");
        var brandId = await CreateBrandForAsync(client, ingredientId, $"B-{suffix}");
        var otherBrand = await CreateBrandForAsync(client, otherIngredientId, $"O-{suffix}");
        var held = await CreateStockAsync(client, brandId, 10, $"held-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var small = await CreateStockAsync(client, brandId, 3, $"small-{suffix}", DateTimeOffset.UtcNow.AddMonths(6));
        var expired = await CreateStockAsync(client, brandId, 10, $"expired-{suffix}", DateTimeOffset.UtcNow.AddDays(-2));
        var discarded = await CreateStockAsync(client, brandId, 10, $"discarded-{suffix}", status: "discarded");
        var other = await CreateStockAsync(client, otherBrand, 10, $"other-{suffix}");
        var consultId = await CreateConsultIdAsync(admin);
        var orderId = await CreateOrderAsync(admin, consultId, ingredientId, 5);

        // Act
        var wrongTotal = await ReplaceAsync(client, orderId, (held, 4));
        var tooMany = await ReplaceAsync(client, orderId, (small, 5));
        var isExpired = await ReplaceAsync(client, orderId, (expired, 5));
        var notActive = await ReplaceAsync(client, orderId, (discarded, 5));
        var wrongIngredient = await ReplaceAsync(client, orderId, (other, 5));
        var missing = await ReplaceAsync(client, orderId, (ClinicSetup.MissingId, 5));
        var twice = await ReplaceAsync(client, orderId, (held, 2), (held, 3));
        var zero = await ReplaceAsync(client, orderId, (held, 5), (small, 0));
        var none = await ReplaceAsync(client, orderId);

        // Assert
        await Assert.That(wrongTotal.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await wrongTotal.Content.ReadAsStringAsync())
            .Contains("The chosen batches add up to 4, but the order is for 5.");
        await Assert.That(tooMany.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await tooMany.Content.ReadAsStringAsync()).Contains($"Only 3 of B-{suffix} at small-{suffix}");
        await Assert.That(isExpired.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await isExpired.Content.ReadAsStringAsync()).Contains("has expired");
        await Assert.That(notActive.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await notActive.Content.ReadAsStringAsync()).Contains("is not active stock");
        await Assert.That(wrongIngredient.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await wrongIngredient.Content.ReadAsStringAsync())
            .Contains("is not a batch of the medication this order is for");
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(twice.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await twice.Content.ReadAsStringAsync()).Contains("Each stock batch can only be chosen once.");
        await Assert.That(zero.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(none.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await none.Content.ReadAsStringAsync()).Contains("Choose at least one stock batch.");
        await Assert.That((await BatchAsync(client, held)).ReservedQuantity).IsEqualTo(5);
        await Assert.That((await BatchAsync(client, small)).ReservedQuantity).IsEqualTo(0);
        await Assert.That((await BatchAsync(client, other)).ReservedQuantity).IsEqualTo(0);
    }

    [Test]
    public async Task ReplaceAllocations_DispensedOrder_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"done-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 10, $"done-{suffix}");
        var consultId = await CreateConsultIdAsync(admin);
        var orderId = await CreateOrderAsync(admin, consultId, ingredientId, 2);
        (await client.PatchAsJsonAsync($"/orders/{orderId}", new { status = "DISPENSED" }, ApiJson.Options))
            .EnsureSuccessStatusCode();

        // Act
        var response = await ReplaceAsync(client, orderId, (stockId, 2));
        var options = await client.GetAsync($"/orders/{orderId}/allocation-options");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("Order is already DISPENSED.");
        await Assert.That(options.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await ReplaceStatusAsync(client, ClinicSetup.MissingId, stockId))
            .IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ReplaceAllocations_IsForPharmacyStaffOnly()
    {
        // Arrange
        var pharmacist = fixture.CreateApiClient();
        await pharmacist.LoginAsync(ClinicApi.PharmacistEmail);
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(pharmacist, suffix);
        var brandId = await CreateBrandForAsync(pharmacist, ingredientId, $"role-{suffix}");
        var early = await CreateStockAsync(pharmacist, brandId, 5, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var late = await CreateStockAsync(pharmacist, brandId, 5, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(9));
        var consultId = await CreateConsultIdAsync(admin);
        var orderId = await CreateOrderAsync(doctor, consultId, ingredientId, 2);

        // Act
        var put = await ReplaceAsync(doctor, orderId, (late, 2));
        var options = await doctor.GetAsync($"/orders/{orderId}/allocation-options");
        var patch = await doctor.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { allocations = new[] { new { stockId = late, quantity = 2 } } },
            ApiJson.Options
        );
        var changes = await doctor.GetAsync($"/medication-stock/{early}/changes");
        var plainPatch = await doctor.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { remarks = "after food" },
            ApiJson.Options
        );

        // Assert
        await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(options.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(patch.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(changes.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(plainPatch.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await BatchAsync(pharmacist, early)).ReservedQuantity).IsEqualTo(2);
        await Assert.That((await BatchAsync(pharmacist, late)).ReservedQuantity).IsEqualTo(0);
    }

    [Test]
    public async Task Dispense_WithChosenBatches_TakesTheUnitsFromThoseBatches()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"pick-{suffix}");
        var early = await CreateStockAsync(client, brandId, 10, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var late = await CreateStockAsync(client, brandId, 10, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(9));
        var consultId = await CreateConsultIdAsync(admin);
        var orderId = await CreateOrderAsync(admin, consultId, ingredientId, 4);

        // Act
        var short_ = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "DISPENSED", allocations = new[] { new { stockId = late, quantity = 3 } } },
            ApiJson.Options
        );
        var afterRefusal = await BatchAsync(client, early);
        var response = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new
            {
                status = "DISPENSED",
                allocations = new[]
                {
                    new { stockId = late, quantity = 3 },
                    new { stockId = early, quantity = 1 },
                },
            },
            ApiJson.Options
        );
        var order = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var parts = Allocations(order);
        var earlyBatch = await BatchAsync(client, early);
        var lateBatch = await BatchAsync(client, late);

        // Assert
        await Assert.That(short_.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(afterRefusal.Quantity).IsEqualTo(10);
        await Assert.That(afterRefusal.ReservedQuantity).IsEqualTo(4);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(order.GetProperty("status").GetString()).IsEqualTo("DISPENSED");
        await Assert.That(parts.Count).IsEqualTo(2);
        await Assert.That(parts[0]).IsEqualTo(($"early-{suffix}", $"pick-{suffix}", 1));
        await Assert.That(parts[1]).IsEqualTo(($"late-{suffix}", $"pick-{suffix}", 3));
        await Assert.That(earlyBatch.Quantity).IsEqualTo(9);
        await Assert.That(earlyBatch.ReservedQuantity).IsEqualTo(0);
        await Assert.That(lateBatch.Quantity).IsEqualTo(7);
        await Assert.That(lateBatch.ReservedQuantity).IsEqualTo(0);
    }

    [Test]
    public async Task ChangeQuantityAndChooseBatches_InOneRequest_UsesTheNewQuantity()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"qty-{suffix}");
        var early = await CreateStockAsync(client, brandId, 10, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var late = await CreateStockAsync(client, brandId, 10, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(9));
        var consultId = await CreateConsultIdAsync(admin);
        var orderId = await CreateOrderAsync(admin, consultId, ingredientId, 4);

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { quantity = 6, allocations = new[] { new { stockId = late, quantity = 6 } } },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await BatchAsync(client, early)).ReservedQuantity).IsEqualTo(0);
        await Assert.That((await BatchAsync(client, late)).ReservedQuantity).IsEqualTo(6);
    }

    [Test]
    public async Task StockChanges_ListsDispensingsWithPatientAndOtherChangesNewestFirst()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"log-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 20, $"log-{suffix}");
        var villageId = await ClinicSetup.CreateVillageAsync(admin);
        var (patientId, visitId) = await ClinicSetup.RegisterPatientAsync(admin, villageId, $"Log patient {suffix}");
        var consultId = await ClinicSetup.CreateConsultAsync(admin, visitId);
        var orderId = await CreateOrderAsync(admin, consultId, ingredientId, 4);
        var pharmacistId = (await client.WhoAmIAsync()).Id;

        // Act
        (await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/adjustments",
            new { quantityChange = -2 },
            ApiJson.Options
        )).EnsureSuccessStatusCode();
        (await client.PatchAsJsonAsync($"/orders/{orderId}", new { status = "DISPENSED" }, ApiJson.Options))
            .EnsureSuccessStatusCode();
        (await client.PatchAsJsonAsync(
            $"/medication-stock/{stockId}",
            new { location = $"moved-{suffix}" },
            ApiJson.Options
        )).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/splits",
            new
            {
                splits = new object[]
                {
                    new { quantity = 10, location = $"child-a-{suffix}", stockStatus = "active" },
                    new { quantity = 4, location = $"child-b-{suffix}", stockStatus = "active" },
                },
            },
            ApiJson.Options
        )).EnsureSuccessStatusCode();
        var all = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/medication-stock/{stockId}/changes",
            ApiJson.Options
        );
        var dispensedOnly = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/medication-stock/{stockId}/changes?dispensedOnly=true",
            ApiJson.Options
        );
        var batches = await client.GetFromJsonAsync<List<JsonElement>>("/medication-stock", ApiJson.Options);
        var childId = ClinicSetup.ReadGuid(
            batches!.Single(x => x.GetProperty("location").GetString() == $"child-a-{suffix}"),
            "id"
        );
        var child = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/medication-stock/{childId}/changes",
            ApiJson.Options
        );
        var missing = await client.GetAsync($"/medication-stock/{ClinicSetup.MissingId}/changes");

        // Assert
        var kinds = all!.Select(x => x.GetProperty("kind").GetString()).ToList();
        await Assert.That(kinds.Take(4)).IsEquivalentTo(new[] { "split_out", "edit", "dispensed", "adjustment" });
        await Assert.That(kinds.Skip(4).All(x => x == "created")).IsTrue();
        await Assert.That(kinds.Count).IsEqualTo(7);
        await Assert.That(all[0].GetProperty("quantityChange").GetInt32()).IsEqualTo(-14);
        await Assert.That(all[0].GetProperty("quantityAfter").GetInt32()).IsEqualTo(0);
        await Assert.That(all[1].GetProperty("field").GetString()).IsEqualTo("location");
        await Assert.That(all[1].GetProperty("newValue").GetString()).IsEqualTo($"moved-{suffix}");
        await Assert.That(all[1].GetProperty("quantityChange").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(all[3].GetProperty("quantityChange").GetInt32()).IsEqualTo(-2);
        await Assert.That(all[3].GetProperty("quantityAfter").GetInt32()).IsEqualTo(18);
        await Assert.That(all[3].GetProperty("dispensing").ValueKind).IsEqualTo(JsonValueKind.Null);

        await Assert.That(dispensedOnly!.Count).IsEqualTo(1);
        var row = dispensedOnly[0];
        var dispensing = row.GetProperty("dispensing");
        await Assert.That(row.GetProperty("kind").GetString()).IsEqualTo("dispensed");
        await Assert.That(row.GetProperty("quantityChange").GetInt32()).IsEqualTo(-4);
        await Assert.That(row.GetProperty("quantityAfter").GetInt32()).IsEqualTo(14);
        await Assert.That(ClinicSetup.ReadGuid(row, "userId")).IsEqualTo(pharmacistId);
        await Assert.That(row.GetProperty("userName").GetString()).IsNotEmpty();
        await Assert.That(ClinicSetup.ReadGuid(dispensing, "orderId")).IsEqualTo(orderId);
        await Assert.That(dispensing.GetProperty("orderQuantity").GetInt32()).IsEqualTo(4);
        await Assert.That(ClinicSetup.ReadGuid(dispensing, "consultId")).IsEqualTo(consultId);
        await Assert.That(ClinicSetup.ReadGuid(dispensing, "visitId")).IsEqualTo(visitId);
        await Assert.That(ClinicSetup.ReadGuid(dispensing, "patientId")).IsEqualTo(patientId);
        await Assert.That(dispensing.GetProperty("patientName").GetString()).IsEqualTo($"Log patient {suffix}");
        await Assert.That(dispensing.GetProperty("patientDisplayId").GetString()).IsNotEmpty();

        await Assert.That(child!.All(x => x.GetProperty("kind").GetString() == "split_in")).IsTrue();
        await Assert.That(child.Count(x => x.GetProperty("field").GetString() == "quantity")).IsEqualTo(1);
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task OrderAndStockLists_CarryTheIngredientUnitAndBrandNames()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix, "mL");
        var brandB = await CreateBrandForAsync(client, ingredientId, $"B-{suffix}");
        var brandA = await CreateBrandForAsync(client, ingredientId, $"A-{suffix}");
        var gone = await CreateBrandForAsync(client, ingredientId, $"gone-{suffix}");
        var stockId = await CreateStockAsync(client, brandB, 50, $"b-{suffix}");
        await CreateStockAsync(client, brandA, 50, $"a-{suffix}");
        await CreateStockAsync(client, gone, 50, $"gone-{suffix}", DateTimeOffset.UtcNow.AddDays(-1));
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 5);

        // Act
        var order = await client.GetFromJsonAsync<JsonElement>($"/orders/{orderId}", ApiJson.Options);
        var queue = await client.GetFromJsonAsync<List<JsonElement>>("/pharmacy-orders", ApiJson.Options);
        var queued = queue!
            .SelectMany(x => x.GetProperty("visits").EnumerateArray())
            .SelectMany(x => x.GetProperty("orders").EnumerateArray())
            .Single(x => ClinicSetup.ReadGuid(x, "orderId") == orderId);
        var stocks = await client.GetFromJsonAsync<List<JsonElement>>("/medication-stock", ApiJson.Options);
        var withBrand = await client.GetFromJsonAsync<List<JsonElement>>(
            "/medication-stock/with-brand",
            ApiJson.Options
        );
        var byIngredient = await client.GetFromJsonAsync<List<JsonElement>>(
            "/medication-stock/by-ingredient",
            ApiJson.Options
        );
        var brandNames = byIngredient!
            .Single(x => ClinicSetup.ReadGuid(x, "ingredientId") == ingredientId)
            .GetProperty("brandNames")
            .EnumerateArray()
            .Select(x => x.GetString())
            .ToList();

        // Assert
        await Assert.That(order.GetProperty("unitOfMeasurement").GetString()).IsEqualTo("mL");
        await Assert.That(queued.GetProperty("unitOfMeasurement").GetString()).IsEqualTo("mL");
        await Assert.That(ClinicSetup.ReadGuid(queued, "medicationActiveIngredientId")).IsEqualTo(ingredientId);
        await Assert.That(
                stocks!.Single(x => ClinicSetup.ReadGuid(x, "id") == stockId)
                    .GetProperty("unitOfMeasurement")
                    .GetString()
            )
            .IsEqualTo("mL");
        await Assert.That(
                withBrand!.Single(x => ClinicSetup.ReadGuid(x, "id") == stockId)
                    .GetProperty("unitOfMeasurement")
                    .GetString()
            )
            .IsEqualTo("mL");
        await Assert.That(brandNames).IsEquivalentTo(new[] { $"A-{suffix}", $"B-{suffix}" });
    }

    private static Task<HttpResponseMessage> ReplaceAsync(
        HttpClient client,
        Guid orderId,
        params (Guid StockId, int Quantity)[] picks
    ) =>
        client.PutAsJsonAsync(
            $"/orders/{orderId}/allocations",
            new { allocations = picks.Select(x => new { stockId = x.StockId, quantity = x.Quantity }) },
            ApiJson.Options
        );

    private static async Task<HttpStatusCode> ReplaceStatusAsync(HttpClient client, Guid orderId, Guid stockId) =>
        (await ReplaceAsync(client, orderId, (stockId, 2))).StatusCode;

    /// <summary>An order's allocations in the order the API lists them: earliest expiry first.</summary>
    private static List<(string? Location, string? Brand, int Quantity)> Allocations(JsonElement order) =>
        order
            .GetProperty("allocations")
            .EnumerateArray()
            .Select(x => (
                x.GetProperty("location").GetString(),
                x.GetProperty("brandName").GetString(),
                x.GetProperty("quantity").GetInt32()
            ))
            .ToList();

    private static async Task<Guid> CreateOrderAsync(
        HttpClient client,
        Guid consultId,
        Guid ingredientId,
        int quantity
    )
    {
        var response = await client.PostAsJsonAsync(
            "/orders",
            new
            {
                consultId,
                medicationActiveIngredientId = ingredientId,
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

    /// <summary>A batch as the stock list shows it; an empty batch is not listed, so it reads as 0.</summary>
    private static async Task<Batch> BatchAsync(HttpClient client, Guid stockId)
    {
        var batches = await client.GetFromJsonAsync<List<Batch>>("/medication-stock", ApiJson.Options);
        return batches!.SingleOrDefault(x => x.Id == stockId) ?? new Batch(stockId, 0, 0, 0);
    }

    private static async Task<Guid> CreateIngredientAsync(
        HttpClient client,
        string suffix,
        string unit = "tablets"
    )
    {
        var ingredient = await client.PostAsJsonAsync(
            "/medication-ingredients",
            new
            {
                name = $"Allocation ingredient {suffix}",
                unitOfMeasurement = unit,
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

    private static async Task<Guid> CreateBrandForAsync(HttpClient client, Guid ingredientId, string name)
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

    private static async Task<Guid> CreateStockAsync(
        HttpClient client,
        Guid brandId,
        int quantity,
        string location,
        DateTimeOffset? expiry = null,
        string status = "active"
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
                stockStatus = status,
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

    private sealed record Batch(Guid Id, int Quantity, int ReservedQuantity, int AvailableQuantity);
}
