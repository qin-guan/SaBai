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

    protected override string[] Args =>
        IsContinuousIntegration() ? ["--WorkOS:UseFake=true"] : [];

    protected override IEnumerable<string> ResourcesToRemove() => ["postgres-pgadmin"];

    protected override ResourceWaitBehavior WaitBehavior => ResourceWaitBehavior.Named;

    protected override IEnumerable<string> ResourcesToWaitFor() => ["api"];

    private static bool IsContinuousIntegration() =>
        IsEnabled(Environment.GetEnvironmentVariable("CI"))
        || IsEnabled(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
        || IsEnabled(Environment.GetEnvironmentVariable("GITLAB_CI"))
        || IsEnabled(Environment.GetEnvironmentVariable("TF_BUILD"));

    private static bool IsEnabled(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
}
