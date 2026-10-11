using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FastEndpoints.Security;
using QuestPDF.Infrastructure;
using SaBai.API.FaceRecognition;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Middleware;
using SaBai.API.OpenApi;
using SaBai.API.Storage;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddFastEndpoints()
    .OpenApiDocument(o =>
    {
        // Strip the invalid `"const": null` that Microsoft.OpenApi emits on cloned
        // request schemas. Runs after FastEndpoints' own document transformer.
        // See BareConstOpenApiCleanup for the full explanation.
        o.ConfigureOpenApi = BareConstOpenApiCleanup.Register;
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<WorkOsSettings>(builder.Configuration.GetSection(WorkOsSettings.SectionName));
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection(AuthSettings.SectionName));

builder.Services.AddSingleton(sp => RustFsS3.Read(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp => RustFsS3.CreateClient(sp.GetRequiredService<RustFsSettings>()));

var auth = builder.Configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>() ?? new AuthSettings();
var workOs = builder.Configuration.GetSection(WorkOsSettings.SectionName).Get<WorkOsSettings>() ?? new WorkOsSettings();
if (auth.JwtSigningKey.Length < 32)
{
    throw new InvalidOperationException("Auth:JwtSigningKey must be at least 32 characters.");
}

if (workOs.UseFake)
{
    builder.Services.AddSingleton<IWorkOsDirectory, FakeWorkOsDirectory>();
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

// The web client is a separate origin (Next.js) and authenticates with a bearer
// token, so it needs an explicit CORS policy. Origins are configurable per
// environment; the local Next.js dev server is allowed by default.
const string webCorsPolicy = "web";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddPolicy(
    webCorsPolicy,
    policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.AddNpgsqlDbContext<AppDbContext>(connectionName: "sabai");
builder.AddQdrantClient("qdrant");
builder.AddFaceRecognition();

var app = builder.Build();
var storage = app.Services.GetRequiredService<RustFsSettings>();
if (!string.IsNullOrWhiteSpace(storage.Endpoint))
{
    await RustFsS3.EnsureBucketAsync(app.Services.GetRequiredService<Amazon.S3.IAmazonS3>(), storage, CancellationToken.None);
}
app.Logger.LogInformation(
    "WorkOS UseFake={UseFake}; ClientId is {ClientIdState}; ApiKey is {ApiKeyState}",
    workOs.UseFake,
    string.IsNullOrWhiteSpace(workOs.ClientId) ? "missing" : "set",
    string.IsNullOrWhiteSpace(workOs.ApiKey) ? "missing" : "set");

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

app.UseCors(webCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(config =>
{
    config.Errors.UseProblemDetails();
    config.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
});

app.Run();
