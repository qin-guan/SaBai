namespace SaBai.API.Domain;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

/// <summary>
/// An insert hit a row that already has its id: typically a create resent with its
/// client-chosen id while the first request was still being saved. The request is run once more, when the
/// create finds the row and answers as a replay (see <see cref="Middleware.ClientIdRetryMiddleware"/>).
/// </summary>
public sealed class DuplicateKeyException(Exception inner)
    : Exception("A record with this id already exists.", inner);

public sealed class BadRequestException(string message) : Exception(message);

public sealed class ForbiddenException(string message) : Exception(message);

public sealed class AuthRejectedException() : Exception("Invalid credentials.");

public sealed class LockedException(string message) : Exception(message);
