using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddFastEndpoints()
    .OpenApiDocument();

builder.AddNpgsqlDbContext<DbContext>(connectionName: "sabai");
builder.AddQdrantClient("qdrant");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapScalarApiReference(
    o =>
    {
        o.AddDocuments("v1");
        o.OperationTitleSource = OperationTitleSource.Path;
    });

app.UseHttpsRedirection();
app.UseFastEndpoints();

app.Run();
