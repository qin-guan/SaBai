using System.Net.Http.Json;
using System.Text.Json;

namespace SaBai.Tests.Support;

public static class ClinicSetup
{
    public static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    public static readonly Guid MissingId = Guid.Parse("01900000-0000-7000-8000-000000999999");

    public static async Task<Guid> CreateVillageAsync(HttpClient client, string? code = null)
    {
        code ??= $"V{Suffix().ToUpperInvariant()}";
        var response = await client.PostAsJsonAsync(
            "/village-codes",
            new
            {
                code,
                name = $"Village {code}",
                colorHex = "#336699",
                isVisible = true,
            },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return ReadGuid(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    public static async Task<(Guid PatientId, Guid VisitId)> RegisterPatientAsync(
        HttpClient client,
        Guid villageId,
        string? name = null
    )
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
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options);
        return (ReadGuid(body, "id"), ReadGuid(body, "visitId"));
    }

    public static async Task<Guid> CreateConsultAsync(
        HttpClient client,
        Guid visitId,
        string details = "cough"
    )
    {
        var response = await client.PostAsJsonAsync(
            "/consults",
            new
            {
                visitId,
                consultation = "exam",
                diagnoses = new[] { new { details, category = "Respiratory" } },
            },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return ReadGuid(
            await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options),
            "id"
        );
    }

    public static Guid ReadGuid(JsonElement element, string name)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.TryGetGuid(out var id)
        )
        {
            return id;
        }

        throw new InvalidOperationException($"Response did not include {name}: {element}");
    }

    public static int ReadInt(JsonElement element, string name)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.TryGetInt32(out var id)
        )
        {
            return id;
        }

        throw new InvalidOperationException($"Response did not include {name}: {element}");
    }
}
