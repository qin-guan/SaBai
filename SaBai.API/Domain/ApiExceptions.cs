namespace SaBai.API.Domain;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

public sealed class BadRequestException(string message) : Exception(message);

public sealed class ForbiddenException(string message) : Exception(message);

public sealed class AuthRejectedException() : Exception("Invalid credentials.");

public sealed class LockedException(string message) : Exception(message);
