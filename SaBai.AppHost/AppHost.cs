using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant").WithDataVolume();
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin(pgAdmin =>
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            // WithPgAdmin excludes this development tool from publishing by default.
            pgAdmin
                .PublishAsContainer()
                .PublishAsDockerComposeService(
                    (_, service) =>
                    {
                        service.Name = "pgadmin";
                        service.Ports = ["5050:80"];
                    }
                );
        }
    });
var db = postgres.AddDatabase("sabai");

var rustFsAccessKey = builder.AddParameter("rustFsAccessKey", "rustfsadmin");
var rustFsSecretKey = builder.AddParameter("rustFsSecretKey", "rustfsadmin", secret: true);
var rustFs = builder
    .AddContainer("fs", "rustfs/rustfs", "latest")
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

var api = builder
    .AddProject<SaBai_API>("api")
    .WithReference(qdrant)
    .WithReference(db)
    .WithReference(s3)
    .WithEnvironment(
        "ConnectionStrings__fs-sabai",
        ReferenceExpression.Create(
            $"Endpoint={s3};AccessKey={rustFsAccessKey};SecretKey={rustFsSecretKey};Bucket=sabai"
        )
    )
    .WaitFor(db)
    .WaitFor(rustFs)
    .WithHttpHealthCheck("/health");

if (builder.ExecutionContext.IsPublishMode)
{
    // Deployments start with one admin account; everyone else is added from the
    // admin pages. The password is only used if the account has none yet.
    api.WithEnvironment(
            "Auth__BootstrapAdmin__UserName",
            builder.AddParameter("adminUserName", "admin")
        )
        .WithEnvironment(
            "Auth__BootstrapAdmin__Password",
            builder.AddParameter("adminPassword", secret: true)
        );
}
else
{
    // Local runs and tests get the demo staff (admin, doctor, pharmacist,
    // registration; password "Password1!"). Opt out with --Auth:SeedDemoUsers=false.
    api.WithEnvironment(
        "Auth__SeedDemoUsers",
        builder.Configuration["Auth:SeedDemoUsers"] is "false" or "0" ? "false" : "true"
    );
    if (builder.Configuration["Auth:SecurityStampValidationMinutes"] is { } interval)
    {
        api.WithEnvironment("Auth__SecurityStampValidationMinutes", interval);
    }
}

if (!builder.ExecutionContext.IsPublishMode)
{
    builder
        .AddNextJsApp("frontend", "../SaBai.Web")
        .WithPnpm(false)
        .WithEnvironment("API_URL", api.GetEndpoint("http"))
        .WithHttpEndpoint(port: 3000, env: "PORT")
        .WaitFor(api);

    builder
        .AddJavaScriptApp("frontend-nuxt", "../SaBai.Web.Nuxt", "dev")
        .WithPnpm(false)
        .WithEnvironment("NUXT_API_URL", api.GetEndpoint("http"))
        .WithHttpEndpoint(port: 3300, env: "PORT")
        .WaitFor(api);
}

if (builder.ExecutionContext.IsPublishMode)
{
    builder
        .AddDockerComposeEnvironment("compose")
        .WithProperties(env =>
        {
            env.DashboardEnabled = true;
        })
        .WithDashboard(dashboard => dashboard.WithHostPort(18888));

    qdrant.PublishAsDockerComposeService(
        (_, service) =>
        {
            service.Name = "qdrant";
            service.Ports = ["6333:6333", "6334:6334"];
        }
    );

    postgres.PublishAsDockerComposeService(
        (_, service) =>
        {
            service.Name = "postgres";
            service.AddEnvironmentalVariable("POSTGRES_DB", "sabai");
        }
    );

    rustFs.PublishAsDockerComposeService(
        (_, service) =>
        {
            service.Name = "fs";
            service.Ports = ["9001:9001"];
        }
    );

    api.PublishAsDockerComposeService(
        (_, service) =>
        {
            service.Name = "api";
            service.Image = null;
            service.Ports = ["8080:8080"];
            service.Build = new Build { Context = ".", Dockerfile = "SaBai.API/Dockerfile" };
        }
    );

    // Build from the repo root so pnpm can resolve the SDK workspace package.
    var frontend = builder
        .AddDockerfile("frontend", "..", "SaBai.Web.Nuxt/Dockerfile")
        .WithEnvironment("NUXT_API_URL", api.GetEndpoint("http"))
        .WithEnvironment("PORT", "3000")
        .WithEnvironment("HOSTNAME", "0.0.0.0")
        .WithHttpEndpoint(port: 3000, targetPort: 3000)
        .WithOtlpExporter()
        .WaitFor(api);


    frontend.PublishAsDockerComposeService(
        (_, service) =>
        {
            service.Name = "frontend";
            service.Image = null;
            service.Ports = ["3000:3000"];
            service.Build = new Build { Context = ".", Dockerfile = "SaBai.Web.Nuxt/Dockerfile" };
        }
    );
}

builder.Build().Run();
