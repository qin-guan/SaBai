using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant");
var postgres = builder.AddPostgres("postgres").WithPgAdmin();
var db = postgres.AddDatabase("sabai");

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddDockerComposeEnvironment("compose")
        .WithProperties(env => { env.DashboardEnabled = true; });
    qdrant.PublishAsDockerComposeService((_, service) => { service.Name = "qdrant"; });
    postgres.PublishAsDockerComposeService((_, service) => { service.Name = "postgres"; });
}

var api = builder.AddProject<SaBai_API>("api")
    .WithReference(qdrant)
    .WithReference(db)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

if (builder.ExecutionContext.IsPublishMode)
{
    api.PublishAsDockerComposeService((_, service) => { service.Name = "api"; });
}

builder.Build().Run();