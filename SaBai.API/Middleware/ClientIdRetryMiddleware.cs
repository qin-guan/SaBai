using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Middleware;

/// <summary>
/// Runs a JSON create once more when its insert hit an existing primary key
/// (<see cref="DuplicateKeyException"/>). A device that timed out waiting for a create resends
/// it with the same client-chosen id; if the first request was still saving, the resend's
/// insert fails on the key once the first commits. Run again, the endpoint finds the row
/// (<see cref="ClientIds.FindExistingAsync{T}(IQueryable{T}, Guid?, Func{T, bool}, CancellationToken)"/>)
/// and answers with it, as for any resent create. A second collision is answered with 409.
/// </summary>
public sealed class ClientIdRetryMiddleware(RequestDelegate next)
{
    /// <summary>Larger bodies (uploads) are not buffered and not retried.</summary>
    private const long MaxBufferedBody = 1024 * 1024;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var request = context.Request;
        if (
            !(HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method))
            || request.HasJsonContentType() is false
            || request.ContentLength is null or > MaxBufferedBody
        )
        {
            await next(context);
            return;
        }

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, context.RequestAborted);
        var body = buffer.ToArray();
        request.Body = new MemoryStream(body, writable: false);

        try
        {
            await next(context);
        }
        catch (DuplicateKeyException) when (!context.Response.HasStarted)
        {
            // Forget the failed insert and read the body afresh.
            db.ChangeTracker.Clear();
            request.Body = new MemoryStream(body, writable: false);
            await next(context);
        }
    }
}
