using System.Net.Http.Json;
using SaBai.API.Endpoints.Auth;

namespace SaBai.Tests.Support;

public static class ClinicApi
{
    public const string AdminEmail = "admin@sabai.test";
    public const string DoctorEmail = "doctor@sabai.test";
    public const string PharmacistEmail = "pharmacist@sabai.test";
    public const string RegistrationEmail = "registration@sabai.test";
    public const string Password = "Password1!";

    /// <summary>
    /// Signs in (by username or email) and attaches the session cookie to every
    /// later request made with this client.
    /// </summary>
    public static async Task<HttpClient> LoginAsync(
        this HttpClient client,
        string userName = AdminEmail,
        string password = Password
    )
    {
        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { userName, password },
            ApiJson.Options
        );
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SignInResponse>(ApiJson.Options);
        if (body is not { RequiresTwoFactor: false })
        {
            throw new InvalidOperationException($"{userName} needs a two-factor code.");
        }

        client.UseCookiesFrom(response);
        return client;
    }

    /// <summary>Copies Set-Cookie values from a response onto the client's Cookie header.</summary>
    public static void UseCookiesFrom(this HttpClient client, HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return;
        }

        var cookies = ReadCookies(client);
        foreach (var setCookie in setCookies)
        {
            var pair = setCookie.Split(';', 2)[0];
            var name = pair.Split('=', 2)[0];
            var expired = setCookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase);
            cookies.Remove(name);
            if (!expired)
            {
                cookies[name] = pair;
            }
        }

        client.DefaultRequestHeaders.Remove("Cookie");
        if (cookies.Count > 0)
        {
            client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies.Values));
        }
    }

    private static Dictionary<string, string> ReadCookies(HttpClient client)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        if (client.DefaultRequestHeaders.TryGetValues("Cookie", out var values))
        {
            foreach (var pair in string.Join("; ", values).Split("; "))
            {
                cookies[pair.Split('=', 2)[0]] = pair;
            }
        }

        return cookies;
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
