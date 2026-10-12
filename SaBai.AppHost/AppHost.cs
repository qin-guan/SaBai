using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant").WithDataVolume();
var postgres = builder.AddPostgres("postgres").WithDataVolume().WithPgAdmin();
var db = postgres.AddDatabase("sabai");

var rustFsAccessKey = builder.AddParameter("rustFsAccessKey", "rustfsadmin");
var rustFsSecretKey = builder.AddParameter("rustFsSecretKey", "rustfsadmin", secret: true);
var rustFs = builder.AddContainer("fs", "rustfs/rustfs", "latest")
    .WithHttpEndpoint(targetPort: 9000, name: "s3")
    .WithHttpEndpoint(targetPort: 9001, name: "console")
    .WithEnvironment("RUSTFS_VOLUMES", "/data/rustfs0")
    .WithEnvironment("RUSTFS_ADDRESS", "0.0.0.0:9000")
    .WithEnvironment("RUSTFS_CONSOLE_ADDRESS", "0.0.0.0:9001")
    .WithEnvironment("RUSTFS_CONSOLE_ENABLE", "true")
    .WithEnvironment("RUSTFS_CORS_ALLOWED_ORIGINS", "*")
    .WithEnvironment("RUSTFS_CONSOLE_CORS_ALLOWED_ORIGINS", "*")
    .WithEnvironment("RUSTFS_ACCESS_KEY", rustFsAccessKey)
    .WithEnvironment("RUSTFS_SECRET_KEY", rustFsSecretKey)
    .WithEnvironment("RUSTFS_OBS_LOGGER_LEVEL", "info")
    .WithVolume("fs-data", "/data")
    .WithVolume("fs-logs", "/app/logs");
var s3 = rustFs.GetEndpoint("s3");

var api = builder.AddProject<SaBai_API>("api")
    .WithReference(qdrant)
    .WithReference(db)
    .WithReference(s3)
    .WithEnvironment("ConnectionStrings__fs-sabai", ReferenceExpression.Create(
        $"Endpoint={s3};AccessKey={rustFsAccessKey};SecretKey={rustFsSecretKey};Bucket=sabai"))
    .WaitFor(db)
    .WaitFor(rustFs)
    .WithHttpHealthCheck("/health");

var useFakeWorkOs = !builder.ExecutionContext.IsPublishMode
                    && builder.Configuration["WorkOS:UseFake"] is "true" or "1";
api.WithEnvironment("WorkOS__UseFake", useFakeWorkOs ? "true" : "false");
if (!useFakeWorkOs)
{
    api.WithEnvironment("WorkOS__ClientId", builder.AddParameter("workOsClientId", "client_01M4K5B4J6E84RQ9DX81J49013"))
        .WithEnvironment("WorkOS__ApiKey", builder.AddParameter("workOsApiKey", secret: true));
}

var frontend = builder.AddNextJsApp("frontend", "../SaBai.Web")
    .WithPnpm(install: false)
    .WithEnvironment("NEXT_PUBLIC_API_URL", api.GetEndpoint("http"))
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WaitFor(api);

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddDockerComposeEnvironment("compose")
        .WithProperties(env =>
        {
            env.DashboardEnabled = true;
        });

    qdrant.PublishAsDockerComposeService((_, service) =>
    {
        service.Name = "qdrant";
        service.Ports = ["9000:9000"];
    });

    postgres.PublishAsDockerComposeService((_, service) =>
    {
        service.Name = "postgres";
        service.AddEnvironmentalVariable("POSTGRES_DB", "sabai");
    });

    rustFs.PublishAsDockerComposeService((_, service) =>
    {
        service.Name = "fs";
        service.Ports = ["9001:9001"];
    });

    api.PublishAsDockerComposeService((_, service) =>
    {
        service.Name = "api";
        service.Image = null;
        service.Ports = ["8080:8080"];
        service.Build = new Build
        {
            Context = ".",
            Dockerfile = "Sabai.API/Dockerfile",
        };
    });

    frontend.PublishAsDockerComposeService((_, service) =>
    {
        service.Name = "frontend";
        service.Image = null;
        service.Ports = ["3000:3000"];
        service.Build = new Build
        {
            Context = ".",
            Dockerfile = "./SaBai.AppHost/aspire-output/frontend.Dockerfile",
        };
    });
}

builder.Build().Run();