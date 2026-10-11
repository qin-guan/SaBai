using Projects;

// Tests pass this because the AppHost process Aspire starts does not inherit CI.
var useFakeWorkOs = IsContinuousIntegration() || HasArg(args, "--use-fake-workos=true");
if (useFakeWorkOs)
{
    // The GitHub runner has no trusted ASP.NET dev cert, so the HTTPS health check cannot connect.
    Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
}

var builder = DistributedApplication.CreateBuilder(args);
useFakeWorkOs |= !builder.ExecutionContext.IsPublishMode && IsEnabled(builder.Configuration["WorkOS:UseFake"]);
if (builder.ExecutionContext.IsPublishMode)
{
    useFakeWorkOs = false;
}

var qdrant = builder.AddQdrant("qdrant");
var postgres = builder.AddPostgres("postgres").WithPgAdmin();
var db = postgres.AddDatabase("sabai");

var rustFsAccessKey = builder.AddParameter("rustFsAccessKey", "rustfsadmin");
var rustFsSecretKey = builder.AddParameter("rustFsSecretKey", "rustfsadmin");
var rustFs = builder.AddRustFs("fs", rustFsAccessKey, rustFsSecretKey);
var bucket = rustFs.AddBucket("sabai");

var api = (useFakeWorkOs
        ? builder.AddProject<SaBai_API>("api", "http")
        : builder.AddProject<SaBai_API>("api"))
    .WithReference(qdrant)
    .WithReference(db)
    .WithReference(bucket)
    .WaitFor(db)
    .WaitFor(bucket)
    .WithHttpHealthCheck("/health");

if (useFakeWorkOs)
{
    api.WithEnvironment("WorkOS__UseFake", "true");
}
else
{
    var workOsClientId = builder.AddParameter("workOsClientId", "client_01M4K5B4J6E84RQ9DX81J49013");
    var workOsApiKey = builder.AddParameter("workOsApiKey", secret: true);
    api.WithEnvironment("WorkOS__ClientId", workOsClientId)
        .WithEnvironment("WorkOS__ApiKey", workOsApiKey);
}

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddDockerComposeEnvironment("compose")
        .WithProperties(env => { env.DashboardEnabled = true; });
    qdrant.PublishAsDockerComposeService((_, service) => { service.Name = "qdrant"; });
    postgres.PublishAsDockerComposeService((_, service) => { service.Name = "postgres"; });
    rustFs.PublishAsDockerComposeService((_, service) => { service.Name = "fs"; });
    api.PublishAsDockerComposeService((_, service) => { service.Name = "api"; });
}

builder.Build().Run();

static bool IsContinuousIntegration() =>
    IsEnabled(Environment.GetEnvironmentVariable("CI"))
    || IsEnabled(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
    || IsEnabled(Environment.GetEnvironmentVariable("GITLAB_CI"))
    || IsEnabled(Environment.GetEnvironmentVariable("TF_BUILD"));

static bool HasArg(string[] args, string expected) =>
    args.Any(arg => string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase));

static bool IsEnabled(string? value) =>
    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";