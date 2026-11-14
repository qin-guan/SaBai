using TUnit.Aspire;

namespace SaBai.Tests.Support;

public class AppFixture : AspireFixture<Projects.SaBai_AppHost>
{
    static AppFixture()
    {
        if (IsContinuousIntegration())
        {
            Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
        }
    }

    protected override TimeSpan ResourceTimeout => TimeSpan.FromMinutes(4);

    // Validate session cookies on every request so lock/role/password changes are
    // observable immediately instead of after the one-minute production interval.
    protected override string[] Args =>
        [
            "--Auth:SeedDemoUsers=true",
            "--Auth:SecurityStampValidationMinutes=0",
            "--Push:Subject=mailto:push@sabai.test",
            $"--Push:PublicKey={PushPublicKey}",
            $"--Push:PrivateKey={PushPrivateKey}",
        ];

    /// <summary>A throwaway VAPID key pair so the push endpoints are switched on in tests.</summary>
    public const string PushPublicKey =
        "BGJ5ooB5kDkrAWZXGRjAgaCS7tiDNwMq38AHbApKnpJP0Fs1ykjqPHgfzq22mtPqPl6XSD5mK0PtZHRzzPUdNIU";

    public const string PushPrivateKey = "ttxHYerkAeg1Mntwzqade39Y7rfv36lhkq9fS4N8-uk";

    protected override IEnumerable<string> ResourcesToRemove() =>
        ["postgres-pgadmin", "frontend", "frontend-nuxt"];

    protected override ResourceWaitBehavior WaitBehavior => ResourceWaitBehavior.Named;

    protected override IEnumerable<string> ResourcesToWaitFor() => ["api"];

    private static bool IsContinuousIntegration() =>
        IsEnabled(Environment.GetEnvironmentVariable("CI"))
        || IsEnabled(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
        || IsEnabled(Environment.GetEnvironmentVariable("GITLAB_CI"))
        || IsEnabled(Environment.GetEnvironmentVariable("TF_BUILD"));

    private static bool IsEnabled(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

    /// <summary>
    /// A client for the API with its own session. Cookies are not stored by the
    /// handler (the shared factory handler would leak one test's session into
    /// another); <see cref="ClinicApi.LoginAsync"/> attaches the session cookie instead.
    /// </summary>
    public HttpClient CreateApiClient()
    {
        var baseAddress = CreateHttpClient("api").BaseAddress;
        return new HttpClient(new SocketsHttpHandler { UseCookies = false })
        {
            BaseAddress = baseAddress,
        };
    }
}
