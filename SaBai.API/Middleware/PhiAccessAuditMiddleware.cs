using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Middleware;

/// <summary>
/// Records HIPAA §164.312(b) audit entries for PHI read, search, and export requests
/// that do not mutate database rows (and therefore bypass <see cref="AuditInterceptor"/>).
/// </summary>
public sealed class PhiAccessAuditMiddleware(RequestDelegate next)
{
    private static readonly (string Prefix, string EntityType)[] PhiPrefixes =
    [
        ("/patients", nameof(Patient)),
        ("/patient-records", nameof(Patient)),
        ("/patient-consult", nameof(Consult)),
        ("/visits", nameof(Visit)),
        ("/vitals", nameof(Vitals)),
        ("/child-vitals", nameof(ChildVitals)),
        ("/puberty", nameof(PubertyAssessment)),
        ("/eyesight", nameof(Eyesight)),
        ("/glasses", nameof(Eyesight)),
        ("/vision", nameof(Eyesight)),
        ("/dental", nameof(DentalConsult)),
        ("/consults", nameof(Consult)),
        ("/diagnoses", nameof(Diagnosis)),
        ("/referrals", nameof(Referral)),
        ("/orders", nameof(Order)),
        ("/pharmacy-orders", nameof(Order)),
        ("/medication-history", nameof(Order)),
        ("/files", nameof(StoredFile)),
        ("/persons", "FaceIdentity"),
        ("/fhir/r4", "FhirResource"),
    ];

    public async Task InvokeAsync(HttpContext context, AuditTrail audit)
    {
        await next(context);

        if (context.Response.StatusCode is < 200 or >= 300)
        {
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var method = context.Request.Method;
        var isRead = HttpMethods.IsGet(method);
        var isFaceSearch =
            HttpMethods.IsPost(method)
            && (
                path.StartsWith("/patients/search-by-face", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/faces/", StringComparison.OrdinalIgnoreCase)
            );

        var entityType = MatchEntityType(path);
        if (entityType is null)
        {
            return;
        }

        // A resent create answered with the existing record: the response discloses it.
        if (!isRead && !isFaceSearch)
        {
            if (ClientIds.ReplayedRecord(context) is { } replayed)
            {
                await audit.RecordAsync(
                    AuditAction.Read,
                    entityType: replayed.EntityType,
                    entityId: replayed.EntityId,
                    ct: context.RequestAborted
                );
            }

            return;
        }

        var isExport =
            path.EndsWith("/pdf", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/content", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/$everything", StringComparison.OrdinalIgnoreCase);

        var action = isExport ? AuditAction.Exported : AuditAction.Read;
        var entityId = ExtractEntityId(path);

        await audit.RecordAsync(
            action,
            entityType: entityType,
            entityId: entityId,
            ct: context.RequestAborted
        );
    }

    private static string? MatchEntityType(string path)
    {
        if (path.Equals("/fhir/r4/metadata", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (var (prefix, entityType) in PhiPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return entityType;
            }
        }

        return null;
    }

    private static string? ExtractEntityId(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (int.TryParse(segment, out _) || Guid.TryParse(segment, out _))
            {
                return segment;
            }
        }

        return null;
    }
}
