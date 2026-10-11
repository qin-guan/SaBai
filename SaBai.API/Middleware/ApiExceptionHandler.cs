using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaBai.API.Domain;

namespace SaBai.API.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException notFound => (StatusCodes.Status404NotFound, "Not Found", notFound.Message),
            ConflictException conflict => (StatusCodes.Status409Conflict, "Conflict", conflict.Message),
            BadRequestException badRequest => (StatusCodes.Status400BadRequest, "Bad Request", badRequest.Message),
            ForbiddenException forbidden => (StatusCodes.Status403Forbidden, "Forbidden", forbidden.Message),
            AuthRejectedException => (StatusCodes.Status401Unauthorized, "Unauthorized", "Invalid credentials."),
            LockedException locked => (StatusCodes.Status405MethodNotAllowed, "Locked", locked.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Conflict", "A record with the same unique value already exists."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                (StatusCodes.Status409Conflict, "Conflict", "The record is still referenced or a related record is missing."),
            _ => (0, null, null),
        };

        if (status == 0)
        {
            logger.LogError(exception, "Unhandled API exception");
            return false;
        }

        logger.LogWarning(exception, "Handled API exception: {Title}", title);
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
            },
            cancellationToken);

        return true;
    }
}
