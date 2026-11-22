using System.Text.Json;
using SaBai.Tests.Support;

namespace SaBai.Tests.OpenApi;

/// <summary>
/// The OpenAPI document the SDK is generated from. It used to fail with 500 when
/// FastEndpoints.OpenApi was built against an older Microsoft.OpenApi than the app loads.
/// </summary>
[ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
public sealed class OpenApiDocumentTests(AppFixture fixture)
{
    [Test]
    public async Task Document_IsServedAndDescribesTheApi()
    {
        // Arrange
        var client = fixture.CreateApiClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");
        await Assert.That(paths.TryGetProperty("/orders", out _)).IsTrue();
        await Assert.That(paths.TryGetProperty("/orders/{id}", out _) || paths.TryGetProperty("/orders/{Id}", out _)).IsTrue();
        await Assert.That(paths.TryGetProperty("/medication-stock/by-ingredient", out _)).IsTrue();
        // Request schemas must not carry the bogus `"const": null` (see BareConstOpenApiCleanup).
        await Assert.That(body).DoesNotContain("\"const\": null");
        await Assert.That(body).DoesNotContain("\"const\":null");
    }
}
