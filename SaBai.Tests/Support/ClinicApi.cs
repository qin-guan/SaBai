using System.Net.Http.Headers;
using System.Net.Http.Json;
using SaBai.API.Endpoints.Auth;
using SaBai.API.Endpoints.Auth.Login.Post;

namespace SaBai.Tests.Support;

public static class ClinicApi
{
    public const string AdminEmail = "admin@sabai.test";
    public const string DoctorEmail = "doctor@sabai.test";
    public const string PharmacistEmail = "pharmacist@sabai.test";
    public const string RegistrationEmail = "registration@sabai.test";
    public const string Password = "Password1!";

    public static async Task<HttpClient> LoginAsync(
        this HttpClient client,
        string email = AdminEmail,
        string password = Password
    )
    {
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { email, password },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Response>(ApiJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body!.AccessToken
        );
        return client;
    }

    public static async Task<UserResponse> WhoAmIAsync(this HttpClient client)
    {
        var response = await client.GetAsync("/auth/whoami");
        response.EnsureSuccessStatusCode();
        var body =
            await response.Content.ReadFromJsonAsync<SaBai.API.Endpoints.Auth.WhoAmI.Get.Response>(
                ApiJson.Options
            );
        return body!.User;
    }
}
