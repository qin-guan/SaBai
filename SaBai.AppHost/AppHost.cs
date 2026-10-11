using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant");
var postgres = builder.AddPostgres("postgres").WithPgAdmin();
var db = postgres.AddDatabase("sabai");

var rustFsAccessKey = builder.AddParameter("rustFsAccessKey", "rustfsadmin");
var rustFsSecretKey = builder.AddParameter("rustFsSecretKey", "rustfsadmin");
var rustFs = builder.AddRustFs("fs", rustFsAccessKey, rustFsSecretKey);
var bucket = rustFs.AddBucket("sabai");

var api = builder.AddProject<SaBai_API>("api")
    .WithReference(qdrant)
    .WithReference(db)
    .WithReference(bucket)
    .WaitFor(db)
    .WaitFor(bucket)
    .WithHttpHealthCheck("/health");

if (IsContinuousIntegration())
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

static bool IsEnabled(string? value) =>
    string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";