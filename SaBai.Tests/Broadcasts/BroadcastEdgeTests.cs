using System.Net.Http.Json;
using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Broadcasts;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("Broadcasts")]
public sealed class BroadcastEdgeTests(AppFixture fixture)
{
    [Test]
    public async Task List_LimitAndSince_PageNewestFirst()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var ids = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            var created = await client.PostAsJsonAsync(
                "/broadcasts",
                new
                {
                    title = $"Page {i} {ClinicSetup.Suffix()}",
                    body = "paging",
                    severity = "info",
                },
                ApiJson.Options
            );
            ids.Add((await created.Content.ReadFromJsonAsync<BroadcastCreated>(ApiJson.Options))!.Id);
        }

        // Act
        var newestTwo = await client.GetFromJsonAsync<List<BroadcastCreated>>(
            "/broadcasts?limit=2",
            ApiJson.Options
        );
        var afterFirst = await client.GetFromJsonAsync<List<BroadcastCreated>>(
            $"/broadcasts?since={ids[0]}",
            ApiJson.Options
        );
        var tooMany = await client.GetAsync("/broadcasts?limit=201");
        var zero = await client.GetAsync("/broadcasts?limit=0");

        // Assert
        await Assert.That(newestTwo!.Select(x => x.Id)).IsEquivalentTo(new[] { ids[2], ids[1] });
        await Assert.That(afterFirst!.Select(x => x.Id).Take(2)).IsEquivalentTo(new[] { ids[2], ids[1] });
        await Assert.That(afterFirst!.All(x => x.Id > ids[0])).IsTrue();
        await Assert.That(tooMany.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(zero.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_ListContainsTitleAndAdminCreator()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var title = $"Alert {ClinicSetup.Suffix()}";

        // Act
        var created = await client.PostAsJsonAsync(
            "/broadcasts",
            new
            {
                title,
                body = "Clinic is closing early.",
                severity = "warning",
            },
            ApiJson.Options
        );
        var broadcast = await created.Content.ReadFromJsonAsync<BroadcastCreated>(ApiJson.Options);
        var listed = await client.GetAsync("/broadcasts");
        var body = await listed.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(broadcast!.CreatedBy).IsEqualTo(ClinicApi.AdminEmail);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains(title);
    }

    [Test]
    public async Task Create_EmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            "/broadcasts",
            new
            {
                title = "",
                body = $"Empty title {ClinicSetup.Suffix()}",
                severity = "info",
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Publish_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();

        // Act
        var response = await client.PostAsync("/broadcasts/99999999/publish", null);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Delete_RemovesTitleFromList()
    {
        // Arrange
        var client = fixture.CreateApiClient();
        await client.LoginAsync();
        var title = $"Remove {ClinicSetup.Suffix()}";
        var created = await client.PostAsJsonAsync(
            "/broadcasts",
            new
            {
                title,
                body = "This notice will be removed.",
                severity = "critical",
            },
            ApiJson.Options
        );
        created.EnsureSuccessStatusCode();
        var id = ClinicSetup.ReadInt(
            await created.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );

        // Act
        var deleted = await client.DeleteAsync($"/broadcasts/{id}");
        var listed = await client.GetAsync("/broadcasts");
        var body = await listed.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(listed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body.Contains(title)).IsFalse();
    }

    [Test]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = fixture.CreateApiClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/broadcasts",
            new
            {
                title = "Closed",
                body = "No token",
                severity = "info",
            },
            ApiJson.Options
        );

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private sealed record BroadcastCreated(int Id, string? CreatedBy);
}
