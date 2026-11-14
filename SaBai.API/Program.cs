using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuestPDF.Infrastructure;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.FaceRecognition;
using SaBai.API.Middleware;
using SaBai.API.OpenApi;
using SaBai.API.Push;
using SaBai.API.Storage;
using Scalar.AspNetCore;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder
    .Services.AddFastEndpoints()
    .OpenApiDocument(o =>
    {
        // Strip the invalid `"const": null` that Microsoft.OpenApi emits on cloned
        // request schemas. Runs after FastEndpoints' own document transformer.
        // See BareConstOpenApiCleanup for the full explanation.
        o.ConfigureOpenApi = BareConstOpenApiCleanup.Register;
        // Sessions are an HttpOnly cookie set by POST /auth/login, not a bearer token.
        o.EnableJWTBearerAuth = false;
        o.AddAuth(
            IdentityConstants.ApplicationScheme,
            new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
                In = Microsoft.OpenApi.ParameterLocation.Cookie,
                Name = SessionCookieName,
                Description = "Session cookie set by `POST /auth/login`.",
            }
        );
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
);

builder.Services.Configure<AuthSettings>(
    builder.Configuration.GetSection(AuthSettings.SectionName)
);

// Web Push notifications to installed copies of the web app. Off until a VAPID
// key pair is configured under "Push".
builder.Services.Configure<PushSettings>(builder.Configuration.GetSection(PushSettings.SectionName));
builder
    .Services.AddHttpClient<PushSender>(client => client.Timeout = TimeSpan.FromSeconds(15))
    // The request URL is the device's push address; keep it out of the logs.
    .RemoveAllLoggers();
builder.Services.AddScoped<PushNotifier>();
builder.Services.AddSingleton<SaBai.API.Sync.SyncTokenService>();
builder.Services.AddScoped<SaBai.API.Sync.SyncUploadProcessor>();

builder.Services.AddSingleton(sp => RustFsS3.Read(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp =>
    RustFsS3.CreateClient(sp.GetRequiredService<RustFsSettings>())
);

var auth =
    builder.Configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>()
    ?? new AuthSettings();

// Staff accounts are ASP.NET Core Identity users stored in Postgres. Identity owns
// password hashing, lockout after failed sign-ins, authenticator (TOTP) two-factor,
// recovery codes and roles. Defaults below can be overridden under "Identity" in
// configuration (for example Identity:Password:RequiredLength).
builder
    .Services.AddIdentityCore<StaffUser>(options =>
    {
        options.User.RequireUniqueEmail = false; // optional; uniqueness enforced by an index
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        builder.Configuration.GetSection("Identity").Bind(options);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Cookie sessions. The web apps reach the API through a same-origin proxy, so an
// HttpOnly, SameSite=Lax cookie is all the browser needs: no token handling in JS.
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = SessionCookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(auth.SessionLifetimeMinutes);
    options.SlidingExpiration = true;
    // An API answers with status codes instead of redirecting to a login page.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
// Re-check cookies against the database this often, so role changes, locks and
// password resets reach sessions that are already signed in.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(auth.SecurityStampValidationMinutes)
);
builder.Services.AddAuthorization();
// Keep the cookie-signing keys in Postgres so sessions survive restarts and are
// shared by every API instance.
builder.Services.AddDataProtection().SetApplicationName("SaBai").PersistKeysToDbContext<AppDbContext>();

// The web apps proxy /api to this service, so browsers normally never make
// cross-origin calls. The policy remains for tools that call the API directly
// from another origin; credentials are allowed so the session cookie is sent.
const string webCorsPolicy = "web";
var allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:3300"];
builder.Services.AddCors(options =>
    options.AddPolicy(
        webCorsPolicy,
        policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
    )
);

// Every row created, updated or deleted through AppDbContext is written to the audit
// log with the signed-in user. The context is pooled, so the interceptor is a single
// shared instance; HttpContextAccessor reads the current request from an AsyncLocal.
builder.Services.AddHttpContextAccessor();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuditTrail>();
var auditInterceptor = new AuditInterceptor(new HttpContextAccessor(), TimeProvider.System);
builder.AddNpgsqlDbContext<AppDbContext>(
    connectionName: "sabai",
    configureDbContextOptions: options => options.AddInterceptors(auditInterceptor)
);
builder.AddQdrantClient("qdrant");
builder.AddFaceRecognition();

var app = builder.Build();
var storage = app.Services.GetRequiredService<RustFsSettings>();
if (!string.IsNullOrWhiteSpace(storage.Endpoint))
{
    await RustFsS3.EnsureBucketAsync(
        app.Services.GetRequiredService<Amazon.S3.IAmazonS3>(),
        storage,
        CancellationToken.None
    );
}

app.UseExceptionHandler();

await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
if (pendingMigrations.Any())
{
    await db.Database.MigrateAsync();
}

await AuthSeeder.EnsureAsync(scope.ServiceProvider, auth, app.Logger);
await SaBai.API.Sync.PowerSyncPublication.EnsureAsync(db, app.Logger);

var push = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<PushSettings>>().Value;
if (!push.IsEnabled)
{
    app.Logger.LogWarning("Push notifications are off: {Reason}", push.Problem);
}

app.MapOpenApi();

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

public partial class Program
{
    /// <summary>Name of the HttpOnly session cookie issued by ASP.NET Core Identity.</summary>
    public const string SessionCookieName = "sabai.auth";
}
