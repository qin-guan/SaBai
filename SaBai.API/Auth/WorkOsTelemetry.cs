using System.Diagnostics;

namespace SaBai.API.Auth;

/// <summary>Spans for WorkOS calls. Registered with OpenTelemetry so Aspire shows them.</summary>
public static class WorkOsTelemetry
{
    public const string SourceName = "SaBai.WorkOS";

    public static readonly ActivitySource Source = new(SourceName);

    public static Activity? Start(string operation, string? subject)
    {
        var activity = Source.StartActivity($"WorkOS {operation}", ActivityKind.Client);
        activity?.SetTag("workos.operation", operation);
        activity?.SetTag("server.address", "api.workos.com");
        if (!string.IsNullOrWhiteSpace(subject))
        {
            activity?.SetTag("workos.subject", subject);
        }

        return activity;
    }

    public static void Fail(Activity? activity, Exception exception)
    {
        var status = Read(exception, "StatusCode");
        var code = Read(exception, "ErrorCode");
        var body = Read(exception, "RawBody");
        if (body is { Length: > 2000 })
        {
            body = body[..2000];
        }

        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("exception.type", exception.GetType().FullName);
        activity?.SetTag("exception.message", exception.Message);
        activity?.SetTag("workos.status_code", status);
        activity?.SetTag("workos.error_code", code);
        activity?.SetTag("workos.response_body", body);
        activity?.AddException(exception);
    }

    private static string? Read(Exception exception, string property) =>
        exception.GetType().GetProperty(property)?.GetValue(exception)?.ToString();
}
