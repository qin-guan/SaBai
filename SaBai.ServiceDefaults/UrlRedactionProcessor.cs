using System.Diagnostics;
using OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Keeps request URLs on exported spans free of PHI: query strings (search terms such as a
/// patient's name) are removed from every span, and a raw <c>url.path</c> is replaced by its route
/// template when a route parameter holds anything other than a GUID.
/// </summary>
/// <remarks>
/// ASP.NET Core and the OpenTelemetry instrumentations only redact query values by default, and
/// Aspire turns that redaction off for local runs; this processor applies regardless.
/// </remarks>
public sealed class UrlRedactionProcessor : BaseProcessor<Activity>
{
    private static readonly string[] FullUrlTags = ["url.full", "http.url", "http.target"];

    public override void OnEnd(Activity activity)
    {
        if (activity.GetTagItem("url.query") is not null)
        {
            activity.SetTag("url.query", null);
        }

        foreach (var tag in FullUrlTags)
        {
            if (activity.GetTagItem(tag) is string url)
            {
                var query = url.IndexOfAny(['?', '#']);
                if (query >= 0)
                {
                    activity.SetTag(tag, url[..query]);
                }
            }
        }

        if (
            activity.GetTagItem("url.path") is string path
            && activity.GetTagItem("http.route") is string { Length: > 0 } route
            && !HasOnlyIdParameters(path, route)
        )
        {
            activity.SetTag("url.path", route);
        }
    }

    /// <summary>
    /// Whether every path segment that fills a route parameter is a GUID, so the raw path can be
    /// kept for correlation without revealing names or identification numbers.
    /// </summary>
    public static bool HasOnlyIdParameters(string path, string route)
    {
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var routeSegments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length != routeSegments.Length)
        {
            return false;
        }

        for (var i = 0; i < pathSegments.Length; i++)
        {
            if (routeSegments[i].Contains('{') && !Guid.TryParse(pathSegments[i], out _))
            {
                return false;
            }
        }

        return true;
    }
}
