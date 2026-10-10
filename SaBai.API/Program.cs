using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FastEndpoints.Security;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddFastEndpoints()
    .OpenApiDocument();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<WorkOsSettings>(builder.Configuration.GetSection(WorkOsSettings.SectionName));
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection(AuthSettings.SectionName));
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection(StorageSettings.SectionName));

var auth = builder.Configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>() ?? new AuthSettings();
var workOs = builder.Configuration.GetSection(WorkOsSettings.SectionName).Get<WorkOsSettings>() ?? new WorkOsSettings();
if (auth.JwtSigningKey.Length < 32)
{
    throw new InvalidOperationException("Auth:JwtSigningKey must be at least 32 characters.");
}

if (workOs.UseFake)
{
    builder.Services.AddSingleton<FakeWorkOsDirectory>();
    builder.Services.AddSingleton<IWorkOsDirectory>(sp => sp.GetRequiredService<FakeWorkOsDirectory>());
}
else
{
    if (string.IsNullOrWhiteSpace(workOs.ApiKey) || string.IsNullOrWhiteSpace(workOs.ClientId))
    {
        throw new InvalidOperationException("WorkOS:ApiKey and WorkOS:ClientId are required when WorkOS:UseFake is false.");
    }

    builder.Services.AddSingleton<IWorkOsDirectory, WorkOsDirectory>();
}

builder.Services
    .AddAuthenticationJwtBearer(options => options.SigningKey = auth.JwtSigningKey)
    .AddAuthorization();

builder.AddNpgsqlDbContext<AppDbContext>(connectionName: "sabai");
builder.AddQdrantClient("qdrant");

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (workOs.UseFake)
    {
        await AuthSeeder.EnsureAsync(db, scope.ServiceProvider.GetRequiredService<FakeWorkOsDirectory>(), CancellationToken.None);
    }

    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapScalarApiReference(options =>
{
    options.AddDocuments("v1");
    options.OperationTitleSource = OperationTitleSource.Path;
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(config =>
{
    config.Errors.UseProblemDetails();
    config.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
});

app.Run();
