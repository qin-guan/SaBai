using System.Net.Http.Json;
using System.Text.Json;

namespace SaBai.Tests.Support;

public static class ClinicSetup
{
    public static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    public static async Task<int> CreateVillageAsync(HttpClient client, string? code = null)
    {
        code ??= $"V{Suffix().ToUpperInvariant()}";
        var response = await client.PostAsJsonAsync(
            "/village-codes",
            new { code, name = $"Village {code}", colorHex = "#336699", isVisible = true },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        return ReadInt(await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
    }

    public static async Task<(int PatientId, int VisitId)> RegisterPatientAsync(
        HttpClient client,
        int villageId,
        string? name = null)
    {
        name ??= $"Patient {Suffix()}";
        var response = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name,
                gender = "female",
                dateOfBirth = new DateTimeOffset(1991, 3, 4, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "None",
                hasPoorCard = false,
                hasBS2Card = false,
                hasSabaiCard = false,
                villageCodeId = villageId,
            },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        return (ReadInt(body, "id"), ReadInt(body, "visitId"));
    }

    public static async Task<int> CreateConsultAsync(HttpClient client, int visitId, string details = "cough")
    {
        var response = await client.PostAsJsonAsync(
            "/consults",
            new
            {
                visitId,
                consultation = "exam",
                diagnoses = new[] { new { details, category = "Respiratory" } },
            },
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        return ReadInt(await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options), "id");
    }

    public static int ReadInt(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.TryGetInt32(out var id))
        {
            return id;
        }

        throw new InvalidOperationException($"Response did not include {name}: {element}");
    }
}
