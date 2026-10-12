using System.Net.Http.Json;
using SaBai.API.Domain;
using SaBai.Tests.Support;
using CreatePatientResponse = SaBai.API.Endpoints.Patients.Create.Post.Response;
using CreateVillageResponse = SaBai.API.Endpoints.VillageCodes.Create.Post.Response;

namespace SaBai.Tests.Patients;

internal static class ClinicSamples
{
    public static string Code() => Guid.NewGuid().ToString("N")[..6];

    public static async Task<CreateVillageResponse> CreateVillageAsync(
        HttpClient client,
        string code,
        bool isVisible = true,
        string colorHex = "#336699",
        string? name = null
    )
    {
        var response = await client.PostAsJsonAsync(
            "/village-codes",
            new
            {
                code,
                name = name ?? $"Village {code}",
                colorHex,
                isVisible,
            },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateVillageResponse>(ApiJson.Options))!;
    }

    public static async Task<CreatePatientResponse> RegisterAsync(
        HttpClient client,
        int villageCodeId,
        string name,
        string? identificationNumber = null,
        bool withVitals = false
    )
    {
        var response = await client.PostAsJsonAsync(
            "/patients",
            new
            {
                name,
                identificationNumber,
                contactNo = "012345678",
                gender = Gender.Female,
                dateOfBirth = new DateTimeOffset(1990, 5, 1, 0, 0, 0, TimeSpan.Zero),
                drugAllergy = "None",
                hasPoorCard = false,
                hasBS2Card = true,
                hasSabaiCard = false,
                toGetReport = true,
                villageCodeId,
                vitals = withVitals
                    ? new
                    {
                        temperature = 36.6m,
                        height = 160m,
                        weight = 55m,
                        systolic = 120,
                        diastolic = 80,
                        heartRate = 72,
                        hemocueCount = 12.5m,
                        diabetesMellitus = false,
                        urineTest = "negative",
                        bloodGlucoseNonFasting = 5.5m,
                        bloodGlucoseFasting = 4.8m,
                        hba1c = 5.4m,
                        others = "none",
                    }
                    : null,
            },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatePatientResponse>(ApiJson.Options))!;
    }
}
