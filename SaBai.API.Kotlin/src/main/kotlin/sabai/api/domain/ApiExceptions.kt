package sabai.api.domain

import org.springframework.http.HttpStatus

/**
 * Exceptions the API turns into problem responses. Each one carries its own status,
 * so [sabai.api.web.ApiExceptionHandler] needs no per-type mapping.
 */
sealed class ApiException(val status: HttpStatus, val title: String, message: String) :
    RuntimeException(message)

class NotFoundException(message: String) : ApiException(HttpStatus.NOT_FOUND, "Not Found", message)

class ConflictException(message: String) : ApiException(HttpStatus.CONFLICT, "Conflict", message)

class BadRequestException(message: String) : ApiException(HttpStatus.BAD_REQUEST, "Bad Request", message)

class ForbiddenException(message: String) : ApiException(HttpStatus.FORBIDDEN, "Forbidden", message)

class AuthRejectedException : ApiException(HttpStatus.UNAUTHORIZED, "Unauthorized", "Invalid credentials.")

/** Kept as 405 to match the C# API (the web app relies on it). */
class LockedException(message: String) : ApiException(HttpStatus.METHOD_NOT_ALLOWED, "Locked", message)

/** A request field failed a check that only the handler can make (e.g. "no face found"). */
class FieldValidationException(
    val field: String,
    val reason: String,
    status: HttpStatus = HttpStatus.BAD_REQUEST,
) : ApiException(status, "One or more validation errors occurred.", reason)
