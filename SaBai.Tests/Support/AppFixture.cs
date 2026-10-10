using TUnit.Aspire;

namespace SaBai.Tests.Support;

public class AppFixture : AspireFixture<Projects.SaBai_AppHost>
{
    protected override TimeSpan ResourceTimeout => TimeSpan.FromMinutes(4);

    protected override IEnumerable<string> ResourcesToRemove() => ["postgres-pgadmin"];

    protected override ResourceWaitBehavior WaitBehavior => ResourceWaitBehavior.Named;

    protected override IEnumerable<string> ResourcesToWaitFor() => ["api"];
}
