using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant");
var postgres = builder.AddPostgres("postgres").WithPgAdmin();
var db = postgres.AddDatabase("sabai");

var rustFsAccessKey = builder.AddParameter("rustFsAccessKey", "rustfsadmin");
var rustFsSecretKey = builder.AddParameter("rustFsSecretKey", "rustfsadmin");
var fs = builder.AddRustFs("fs", rustFsAccessKey, rustFsSecretKey).AddBucket("sabai");

var api = builder.AddProject<SaBai_API>("api")
    .WithReference(qdrant)
    .WithReference(db)
    .WithReference(fs)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddDockerComposeEnvironment("compose")
        .WithProperties(env => { env.DashboardEnabled = true; });
    qdrant.PublishAsDockerComposeService((_, service) => { service.Name = "qdrant"; });
    postgres.PublishAsDockerComposeService((_, service) => { service.Name = "postgres"; });
    api.PublishAsDockerComposeService((_, service) => { service.Name = "api"; });
}

builder.Build().Run();