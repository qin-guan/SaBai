using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Pharmacy;

/// <summary>
/// The order lifecycle against stock batches: placing reserves (earliest expiry first, across
/// brands), changing a pending order re-allocates, cancelling and deleting release, dispensing
/// takes the units off the shelf. Plus the rules for editing a batch that holds reservations.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class OrderEdgeTests(AppFixture fixture)
{
    [Test]
    public async Task Create_UnknownConsult_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var ingredientId = await CreateIngredientAsync(client, ClinicSetup.Suffix());

        // Act
        var response = await PlaceAsync(client, ClinicSetup.MissingId, ingredientId, 1);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_UnknownIngredient_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var response = await PlaceAsync(client, consultId, ClinicSetup.MissingId, 1);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Create_QuantityZero_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var ingredientId = await CreateIngredientAsync(client, ClinicSetup.Suffix());
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var response = await PlaceAsync(client, consultId, ingredientId, 0);

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
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"short-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 1, $"short-{suffix}");
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var response = await PlaceAsync(client, consultId, ingredientId, 5);
        var body = await response.Content.ReadAsStringAsync();
        var batch = await BatchAsync(client, stockId);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(body).Contains("Insufficient stock: 1 of 5 available.");
        await Assert.That(batch.ReservedQuantity).IsEqualTo(0);
        await Assert.That(await AvailableAsync(client, ingredientId)).IsEqualTo(1);
    }

    [Test]
    public async Task Create_ReservesEarliestExpiryFirstAcrossBrands()
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
        await CreateStockAsync(client, brandA, 5, $"middle-{suffix}", DateTimeOffset.UtcNow.AddMonths(5));
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var response = await PlaceAsync(client, consultId, ingredientId, 12);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var parts = Allocations(order);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(parts.Count).IsEqualTo(3);
        await Assert.That(parts[0]).IsEqualTo(($"early-{suffix}", $"B-{suffix}", 5));
        await Assert.That(parts[1]).IsEqualTo(($"middle-{suffix}", $"A-{suffix}", 5));
        await Assert.That(parts[2]).IsEqualTo(($"late-{suffix}", $"A-{suffix}", 2));
        await Assert.That(order.GetProperty("status").GetString()).IsEqualTo("PENDING");
        await Assert.That(order.TryGetProperty("medicationBrandId", out _)).IsFalse();
        await Assert.That(order.TryGetProperty("brandName", out _)).IsFalse();
        await Assert.That(await AvailableAsync(client, ingredientId)).IsEqualTo(3);
    }

    [Test]
    public async Task Create_PartOfABatch_LeavesTheRestAvailable()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"part-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 10, $"part-{suffix}");
        var consultId = await CreateConsultIdAsync(client);

        // Act
        var first = await PlaceAsync(client, consultId, ingredientId, 3);
        var second = await PlaceAsync(client, consultId, ingredientId, 7);
        var batch = await BatchAsync(client, stockId);
        var third = await PlaceAsync(client, consultId, ingredientId, 1);

        // Assert
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(batch.Quantity).IsEqualTo(10);
        await Assert.That(batch.ReservedQuantity).IsEqualTo(10);
        await Assert.That(batch.AvailableQuantity).IsEqualTo(0);
        await Assert.That(third.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Create_ManyOrdersRacingForTheLastUnits_ExactlyOneWins()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"race-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 5, $"race-{suffix}");
        var consultId = await CreateConsultIdAsync(client);

        // Act: six requests at once each ask for 3 of the 5 units, so only one can be covered.
        var results = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => PlaceAsync(client, consultId, ingredientId, 3))
        );
        var losers = results.Where(x => x.StatusCode == HttpStatusCode.Conflict).ToList();
        var loserBodies = await Task.WhenAll(losers.Select(x => x.Content.ReadAsStringAsync()));
        var batch = await BatchAsync(client, stockId);

        // Assert
        await Assert.That(results.Count(x => x.StatusCode == HttpStatusCode.Created)).IsEqualTo(1);
        await Assert.That(losers.Count).IsEqualTo(5);
        await Assert.That(loserBodies.All(x => x.Contains("Insufficient stock: 2 of 3 available."))).IsTrue();
        await Assert.That(batch.ReservedQuantity).IsEqualTo(3);
        await Assert.That(batch.Quantity).IsEqualTo(5);
    }

    [Test]
    public async Task ByIngredient_ListsZeroStockAndCountsOnlyActiveUnexpiredUnreservedUnits()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var emptyIngredient = await CreateIngredientAsync(client, $"empty-{suffix}");
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"avail-{suffix}");
        await CreateStockAsync(client, brandId, 5, $"good-{suffix}");
        await CreateStockAsync(client, brandId, 4, $"expired-{suffix}", DateTimeOffset.UtcNow.AddDays(-1));
        await CreateStockAsync(client, brandId, 3, $"discarded-{suffix}", status: "discarded");
        var consultId = await CreateConsultIdAsync(client);
        (await PlaceAsync(client, consultId, ingredientId, 2)).EnsureSuccessStatusCode();

        // Act
        var rows = await client.GetFromJsonAsync<List<JsonElement>>(
            "/medication-stock/by-ingredient",
            ApiJson.Options
        );
        var empty = rows!.Single(x => ClinicSetup.ReadGuid(x, "ingredientId") == emptyIngredient);
        var stocked = rows!.Single(x => ClinicSetup.ReadGuid(x, "ingredientId") == ingredientId);
        var orderEmpty = await PlaceAsync(client, consultId, emptyIngredient, 1);

        // Assert
        await Assert.That(empty.GetProperty("availableQuantity").GetInt32()).IsEqualTo(0);
        await Assert.That(empty.GetProperty("unitOfMeasurement").GetString()).IsEqualTo("tablets");
        await Assert.That(stocked.GetProperty("availableQuantity").GetInt32()).IsEqualTo(3);
        await Assert.That(orderEmpty.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Update_QuantityUp_ReservesMoreFromTheNextBatch()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"up-{suffix}");
        var early = await CreateStockAsync(client, brandId, 4, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var late = await CreateStockAsync(client, brandId, 6, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(6));
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 3);

        // Act
        var updated = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { quantity = 7, dosageInstructions = "2 tablets daily" },
            ApiJson.Options
        );
        var order = await updated.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(order.GetProperty("quantity").GetInt32()).IsEqualTo(7);
        await Assert.That(order.GetProperty("dosageInstructions").GetString()).IsEqualTo("2 tablets daily");
        await Assert.That(Allocations(order)).IsEquivalentTo(
            new[] { ($"early-{suffix}", $"up-{suffix}", 4), ($"late-{suffix}", $"up-{suffix}", 3) }
        );
        await Assert.That((await BatchAsync(client, early)).ReservedQuantity).IsEqualTo(4);
        await Assert.That((await BatchAsync(client, late)).ReservedQuantity).IsEqualTo(3);
    }

    [Test]
    public async Task Update_QuantityDown_ReleasesTheSurplus()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"down-{suffix}");
        var early = await CreateStockAsync(client, brandId, 4, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var late = await CreateStockAsync(client, brandId, 6, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(6));
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 8);

        // Act
        var updated = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { quantity = 2 },
            ApiJson.Options
        );
        var order = await updated.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Allocations(order)).IsEquivalentTo(
            new[] { ($"early-{suffix}", $"down-{suffix}", 2) }
        );
        await Assert.That((await BatchAsync(client, early)).ReservedQuantity).IsEqualTo(2);
        await Assert.That((await BatchAsync(client, late)).ReservedQuantity).IsEqualTo(0);
        await Assert.That(await AvailableAsync(client, ingredientId)).IsEqualTo(8);
    }

    [Test]
    public async Task Update_Ingredient_MovesTheReservation()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var oldIngredient = await CreateIngredientAsync(client, $"old-{suffix}");
        var newIngredient = await CreateIngredientAsync(client, $"new-{suffix}");
        var oldStock = await CreateStockAsync(
            client,
            await CreateBrandForAsync(client, oldIngredient, $"old-{suffix}"),
            5,
            $"old-{suffix}"
        );
        var newStock = await CreateStockAsync(
            client,
            await CreateBrandForAsync(client, newIngredient, $"new-{suffix}"),
            5,
            $"new-{suffix}"
        );
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var consultId = await CreateConsultIdAsync(admin);
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var orderId = await CreateOrderAsync(doctor, consultId, oldIngredient, 4);

        // Act: the pharmacist swaps the ingredient (and the amount).
        var updated = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { medicationActiveIngredientId = newIngredient, quantity = 3 },
            ApiJson.Options
        );
        var order = await updated.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(ClinicSetup.ReadGuid(order, "medicationActiveIngredientId")).IsEqualTo(newIngredient);
        await Assert.That(order.GetProperty("activeIngredientName").GetString()).IsEqualTo($"Order ingredient new-{suffix}");
        await Assert.That(Allocations(order)).IsEquivalentTo(new[] { ($"new-{suffix}", $"new-{suffix}", 3) });
        await Assert.That((await BatchAsync(client, oldStock)).ReservedQuantity).IsEqualTo(0);
        await Assert.That((await BatchAsync(client, newStock)).ReservedQuantity).IsEqualTo(3);
    }

    [Test]
    public async Task Update_MoreThanAvailable_ReturnsConflictAndChangesNothing()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"over-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 5, $"over-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 2);

        // Act
        var updated = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { quantity = 6, remarks = "should not stick" },
            ApiJson.Options
        );
        var body = await updated.Content.ReadAsStringAsync();
        var order = await client.GetFromJsonAsync<JsonElement>($"/orders/{orderId}", ApiJson.Options);

        // Assert
        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(body).Contains("Insufficient stock: 5 of 6 available.");
        await Assert.That(order.GetProperty("quantity").GetInt32()).IsEqualTo(2);
        await Assert.That(order.GetProperty("remarks").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(Allocations(order)).IsEquivalentTo(new[] { ($"over-{suffix}", $"over-{suffix}", 2) });
        await Assert.That((await BatchAsync(client, stockId)).ReservedQuantity).IsEqualTo(2);
    }

    [Test]
    public async Task Cancel_ReleasesReservedStock()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"cancel-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 10, $"cancel-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 2);
        var heldWhilePending = await AvailableAsync(client, ingredientId);

        // Act
        var cancelled = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "CANCELLED" },
            ApiJson.Options
        );
        var order = await cancelled.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);

        // Assert
        await Assert.That(cancelled.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(order.GetProperty("status").GetString()).IsEqualTo("CANCELLED");
        await Assert.That(order.GetProperty("allocations").GetArrayLength()).IsEqualTo(0);
        await Assert.That(heldWhilePending).IsEqualTo(8);
        await Assert.That(await AvailableAsync(client, ingredientId)).IsEqualTo(10);
        await Assert.That((await BatchAsync(client, stockId)).ReservedQuantity).IsEqualTo(0);
    }

    [Test]
    public async Task Delete_PendingOrder_ReleasesItsStock()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"delete-{suffix}");
        await CreateStockAsync(client, brandId, 6, $"delete-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 4);
        var held = await AvailableAsync(client, ingredientId);

        // Act
        var deleted = await client.DeleteAsync($"/orders/{orderId}");
        var afterDelete = await AvailableAsync(client, ingredientId);
        var gone = await client.GetAsync($"/orders/{orderId}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(held).IsEqualTo(2);
        await Assert.That(afterDelete).IsEqualTo(6);
        await Assert.That(gone.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Dispense_TakesUnitsOffTheShelfAndKeepsAllocations()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync(ClinicApi.PharmacistEmail);
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"give-{suffix}");
        var early = await CreateStockAsync(client, brandId, 3, $"early-{suffix}", DateTimeOffset.UtcNow.AddMonths(2));
        var late = await CreateStockAsync(client, brandId, 10, $"late-{suffix}", DateTimeOffset.UtcNow.AddMonths(6));
        var admin = fixture.CreateApiClient();
        await admin.LoginAsync();
        var consultId = await CreateConsultIdAsync(admin);
        var doctor = fixture.CreateApiClient();
        await doctor.LoginAsync(ClinicApi.DoctorEmail);
        var orderId = await CreateOrderAsync(doctor, consultId, ingredientId, 5);

        // Act
        var dispensed = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "DISPENSED" },
            ApiJson.Options
        );
        var order = await dispensed.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        var earlyBatch = await BatchAsync(client, early);
        var lateBatch = await BatchAsync(client, late);
        var history = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/medication-history?brandId={brandId}",
            ApiJson.Options
        );
        var queue = await client.GetStringAsync("/pharmacy-orders");

        // Assert
        await Assert.That(dispensed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(order.GetProperty("status").GetString()).IsEqualTo("DISPENSED");
        await Assert.That(order.GetProperty("dispensedAt").ValueKind).IsEqualTo(JsonValueKind.String);
        await Assert.That(order.GetProperty("dispensedById").ValueKind).IsEqualTo(JsonValueKind.String);
        await Assert.That(Allocations(order)).IsEquivalentTo(
            new[] { ($"early-{suffix}", $"give-{suffix}", 3), ($"late-{suffix}", $"give-{suffix}", 2) }
        );
        // The early batch is now empty, so it drops out of the list; the late one has 8 left.
        await Assert.That(earlyBatch.Quantity).IsEqualTo(0);
        await Assert.That(lateBatch.Quantity).IsEqualTo(8);
        await Assert.That(lateBatch.ReservedQuantity).IsEqualTo(0);
        await Assert.That(await AvailableAsync(client, ingredientId)).IsEqualTo(8);
        // One "order" row per batch dispensed from, and no duplicate "adjustment" row for it.
        var orderRows = history!.Where(x => x.GetProperty("kind").GetString() == "order").ToList();
        var adjustments = history!
            .Where(x => x.GetProperty("kind").GetString() == "adjustment")
            .Select(x => x.GetProperty("quantityChanged").GetInt32())
            .Order()
            .ToList();
        await Assert.That(orderRows.Select(x => x.GetProperty("quantityChanged").GetInt32()).Order())
            .IsEquivalentTo(new[] { -3, -2 });
        await Assert.That(adjustments).IsEquivalentTo(new[] { 3, 10 });
        await Assert.That(queue).DoesNotContain(orderId.ToString());
    }

    [Test]
    [Arguments("DISPENSED")]
    [Arguments("CANCELLED")]
    public async Task Update_FinishedOrder_ReturnsBadRequestAndChangesNothing(string status)
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"done-{suffix}");
        await CreateStockAsync(client, brandId, 10, $"done-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 2);
        (
            await client.PatchAsJsonAsync($"/orders/{orderId}", new { status }, ApiJson.Options)
        ).EnsureSuccessStatusCode();
        var availableBefore = await AvailableAsync(client, ingredientId);

        // Act
        var changeQuantity = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { quantity = 3 },
            ApiJson.Options
        );
        var changeNotes = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { remarks = "late note" },
            ApiJson.Options
        );
        var again = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "DISPENSED" },
            ApiJson.Options
        );
        var againBody = await again.Content.ReadAsStringAsync();
        var order = await client.GetFromJsonAsync<JsonElement>($"/orders/{orderId}", ApiJson.Options);

        // Assert
        await Assert.That(changeQuantity.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(changeNotes.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(againBody).Contains($"Order is already {status}.");
        await Assert.That(order.GetProperty("quantity").GetInt32()).IsEqualTo(2);
        await Assert.That(order.GetProperty("remarks").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(await AvailableAsync(client, ingredientId)).IsEqualTo(availableBefore);
    }

    [Test]
    public async Task Delete_DispensedOrder_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"keep-{suffix}");
        await CreateStockAsync(client, brandId, 5, $"keep-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 2);
        (
            await client.PatchAsJsonAsync($"/orders/{orderId}", new { status = "DISPENSED" }, ApiJson.Options)
        ).EnsureSuccessStatusCode();

        // Act
        var deleted = await client.DeleteAsync($"/orders/{orderId}");
        var stillThere = await client.GetAsync($"/orders/{orderId}");

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(stillThere.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ReservedBatch_ManualEdits_KeepTheReservationIntact()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"held-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 5, $"held-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        await CreateOrderAsync(client, consultId, ingredientId, 3);

        // Act
        var belowReserved = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/adjustments",
            new { quantityChange = -3 },
            ApiJson.Options
        );
        var belowReservedBody = await belowReserved.Content.ReadAsStringAsync();
        var withinAvailable = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/adjustments",
            new { quantityChange = -2 },
            ApiJson.Options
        );
        var discard = await client.PatchAsJsonAsync(
            $"/medication-stock/{stockId}",
            new { stockStatus = "discarded" },
            ApiJson.Options
        );
        var discardBody = await discard.Content.ReadAsStringAsync();
        var move = await client.PatchAsJsonAsync(
            $"/medication-stock/{stockId}",
            new { location = $"moved-{suffix}" },
            ApiJson.Options
        );
        var batch = await BatchAsync(client, stockId);

        // Assert
        await Assert.That(belowReserved.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(belowReservedBody).Contains("reserved for pending orders");
        await Assert.That(withinAvailable.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(discard.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(discardBody).Contains("reserved for pending orders");
        await Assert.That(move.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(batch.Quantity).IsEqualTo(3);
        await Assert.That(batch.ReservedQuantity).IsEqualTo(3);
        await Assert.That(batch.AvailableQuantity).IsEqualTo(0);
        await Assert.That(batch.StockStatus).IsEqualTo("active");
        await Assert.That(batch.Location).IsEqualTo($"moved-{suffix}");
    }

    [Test]
    public async Task Split_PartlyReservedBatch_MovesOnlyUnreservedUnits()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"split-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 10, $"split-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        var orderId = await CreateOrderAsync(client, consultId, ingredientId, 4);

        // Act
        var whole = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/splits",
            new
            {
                splits = new object[]
                {
                    new { quantity = 5, location = $"all-a-{suffix}", stockStatus = "active" },
                    new { quantity = 5, location = $"all-b-{suffix}", stockStatus = "active" },
                },
            },
            ApiJson.Options
        );
        var wholeBody = await whole.Content.ReadAsStringAsync();
        var split = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/splits",
            new
            {
                splits = new object[]
                {
                    new { quantity = 4, location = $"left-{suffix}", stockStatus = "active" },
                    new { quantity = 2, location = $"right-{suffix}", stockStatus = "donated" },
                },
            },
            ApiJson.Options
        );
        var parent = await BatchAsync(client, stockId);
        var batches = await client.GetFromJsonAsync<List<Batch>>("/medication-stock", ApiJson.Options);
        var left = batches!.Single(x => x.Location == $"left-{suffix}");
        var right = batches!.Single(x => x.Location == $"right-{suffix}");
        var dispensed = await client.PatchAsJsonAsync(
            $"/orders/{orderId}",
            new { status = "DISPENSED" },
            ApiJson.Options
        );
        var parentAfterDispense = await client.GetFromJsonAsync<List<Batch>>(
            "/medication-stock",
            ApiJson.Options
        );

        // Assert
        await Assert.That(whole.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(wholeBody).Contains("6 unreserved units");
        await Assert.That(split.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(parent.Quantity).IsEqualTo(4);
        await Assert.That(parent.ReservedQuantity).IsEqualTo(4);
        await Assert.That(left.Quantity).IsEqualTo(4);
        await Assert.That(left.ParentStockId).IsEqualTo(stockId);
        await Assert.That(right.ParentStockId).IsEqualTo(stockId);
        await Assert.That(await AvailableAsync(client, ingredientId)).IsEqualTo(4);
        await Assert.That(dispensed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(parentAfterDispense!.Any(x => x.Id == stockId)).IsFalse();
    }

    [Test]
    public async Task Split_FullyReservedBatch_ReturnsConflict()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandId = await CreateBrandForAsync(client, ingredientId, $"full-{suffix}");
        var stockId = await CreateStockAsync(client, brandId, 4, $"full-{suffix}");
        var consultId = await CreateConsultIdAsync(client);
        await CreateOrderAsync(client, consultId, ingredientId, 4);

        // Act
        var split = await client.PostAsJsonAsync(
            $"/medication-stock/{stockId}/splits",
            new
            {
                splits = new object[]
                {
                    new { quantity = 2, location = $"a-{suffix}", stockStatus = "active" },
                    new { quantity = 2, location = $"b-{suffix}", stockStatus = "active" },
                },
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(split.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That((await BatchAsync(client, stockId)).ReservedQuantity).IsEqualTo(4);
    }

    [Test]
    public async Task PharmacyOrders_ShowsIngredientAndWhereToTakeIt()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var suffix = ClinicSetup.Suffix();
        var ingredientId = await CreateIngredientAsync(client, suffix);
        var brandName = $"Queue brand {suffix}";
        var brandId = await CreateBrandForAsync(client, ingredientId, brandName);
        var location = $"queue-shelf-{suffix}";
        await CreateStockAsync(client, brandId, 5, location);
        var consultId = await CreateConsultIdAsync(client);
        await CreateOrderAsync(client, consultId, ingredientId, 1);

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
        var response = await PlaceAsync(client, ClinicSetup.MissingId, ClinicSetup.MissingId, 1);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
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

        object ConsultWith(Guid visitId, params int[] quantities) =>
            new
            {
                visitId,
                consultation = "exam",
                diagnoses = new[] { new { details = "cough", category = "Respiratory" } },
                orders = quantities
                    .Select(q => new
                    {
                        medicationActiveIngredientId = ingredientId,
                        quantity = q,
                        dosageInstructions = "1 daily",
                    })
                    .ToArray(),
            };

        // Act: the first consult's two orders take 4 of the 5 units between them; the second
        // consult's first order would fit but its second can't, so the whole consult is refused.
        var covered = await client.PostAsJsonAsync("/consults", ConsultWith(okVisit, 3, 1), ApiJson.Options);
        var afterCovered = await AvailableAsync(client, ingredientId);
        var uncovered = await client.PostAsJsonAsync("/consults", ConsultWith(badVisit, 1, 4), ApiJson.Options);
        var consultsForBadVisit = await client.GetAsync($"/consults?visitId={badVisit}");

        // Assert
        await Assert.That(covered.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(afterCovered).IsEqualTo(1);
        await Assert.That(uncovered.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await AvailableAsync(client, ingredientId)).IsEqualTo(1);
        await Assert.That(await consultsForBadVisit.Content.ReadAsStringAsync()).DoesNotContain(badVisit.ToString());
    }

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

    private static Task<HttpResponseMessage> PlaceAsync(
        HttpClient client,
        Guid consultId,
        Guid ingredientId,
        int quantity
    ) =>
        client.PostAsJsonAsync(
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

    private static async Task<Guid> CreateOrderAsync(
        HttpClient client,
        Guid consultId,
        Guid ingredientId,
        int quantity
    )
    {
        var response = await PlaceAsync(client, consultId, ingredientId, quantity);
        response.EnsureSuccessStatusCode();
        return ClinicSetup.ReadGuid(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    /// <summary>Orderable units of an ingredient, from the doctor's by-ingredient list.</summary>
    private static async Task<int> AvailableAsync(HttpClient client, Guid ingredientId)
    {
        var rows = await client.GetFromJsonAsync<List<JsonElement>>(
            "/medication-stock/by-ingredient",
            ApiJson.Options
        );
        return rows!
            .Single(x => ClinicSetup.ReadGuid(x, "ingredientId") == ingredientId)
            .GetProperty("availableQuantity")
            .GetInt32();
    }

    /// <summary>A batch as the stock list shows it; an empty batch is not listed, so it reads as 0.</summary>
    private static async Task<Batch> BatchAsync(HttpClient client, Guid stockId)
    {
        var batches = await client.GetFromJsonAsync<List<Batch>>("/medication-stock", ApiJson.Options);
        return batches!.SingleOrDefault(x => x.Id == stockId)
            ?? new Batch(stockId, 0, 0, 0, "", "", null);
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

    private sealed record Batch(
        Guid Id,
        int Quantity,
        int ReservedQuantity,
        int AvailableQuantity,
        string Location,
        string StockStatus,
        Guid? ParentStockId
    );
}
