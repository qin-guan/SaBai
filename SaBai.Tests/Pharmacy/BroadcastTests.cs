using System.Net.Http.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.Pharmacy;

[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class BroadcastTests(AppFixture fixture)
{
    [Test]
    public async Task Create_ThenPublish_SetsPublishedAt()
    {
        // Arrange
        var client = fixture.CreateHttpClient("api");
        await client.LoginAsync(ClinicApi.AdminEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var title = $"Broadcast {suffix}";

        // Act
        var created = await client.PostAsJsonAsync(
            "/broadcasts",
            new { title, body = "Clinic is closing early.", severity = "warning" },
            ApiJson.Options);
        var broadcast = await created.Content.ReadFromJsonAsync<BroadcastDto>(ApiJson.Options);
        var published = await client.PostAsync($"/broadcasts/{broadcast!.Id}/publish", null);
        var after = await published.Content.ReadFromJsonAsync<BroadcastDto>(ApiJson.Options);
        var listed = await client.GetFromJsonAsync<List<BroadcastDto>>("/broadcasts", ApiJson.Options);

        // Assert
        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(broadcast.PublishedAt.HasValue).IsFalse();
        await Assert.That(broadcast.CreatedBy).IsEqualTo(ClinicApi.AdminEmail);
        await Assert.That(published.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(after!.PublishedAt.HasValue).IsTrue();
        await Assert.That(listed!.Any(x => x.Id == broadcast.Id && x.Title == title && x.PublishedAt is not null)).IsTrue();
    }

    private sealed record BroadcastDto(int Id, string Title, string? CreatedBy, DateTimeOffset? PublishedAt);
}
