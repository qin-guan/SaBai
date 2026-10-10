using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose")
    .WithProperties(env => { env.DashboardEnabled = true; });

var qdrant = builder.AddQdrant("qdrant")
    .PublishAsDockerComposeService((resource, service) => { service.Name = "qdrant"; });

var postgres = builder.AddPostgres("postgres").WithPgAdmin();

var db = postgres.PublishAsDockerComposeService((resource, service) => { service.Name = "postgres"; })
    .AddDatabase("sabai");

var api = builder.AddProject<SaBai_API>("api")
    .WithReference(qdrant)
    .WithReference(db)
    .PublishAsDockerComposeService((resource, service) => { service.Name = "api"; });

builder.Build().Run();