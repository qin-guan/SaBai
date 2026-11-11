package sabai.api.web

import jakarta.servlet.http.HttpServletRequest
import org.slf4j.LoggerFactory
import org.springframework.dao.DataIntegrityViolationException
import org.springframework.http.HttpHeaders
import org.springframework.http.HttpStatus
import org.springframework.http.HttpStatusCode
import org.springframework.http.ProblemDetail
import org.springframework.http.ResponseEntity
import org.springframework.http.converter.HttpMessageNotReadableException
import org.springframework.web.bind.MethodArgumentNotValidException
import org.springframework.web.bind.annotation.ExceptionHandler
import org.springframework.web.bind.annotation.RestControllerAdvice
import org.springframework.web.context.request.WebRequest
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler
import sabai.api.domain.ApiException
import sabai.api.domain.FieldValidationException
import tools.jackson.databind.DatabindException
import java.net.URI
import java.sql.SQLException

/**
 * The C# ApiExceptionHandler plus FastEndpoints' validation responses. The base class
 * already turns framework errors (bad JSON, wrong method, missing parameter...) into
 * RFC 9457 problem details.
 */
@RestControllerAdvice
class ApiExceptionHandler : ResponseEntityExceptionHandler() {
    private val log = LoggerFactory.getLogger(javaClass)

    @ExceptionHandler
    fun handle(ex: ApiException, request: HttpServletRequest): ProblemDetail {
        log.warn("Handled API exception: {}", ex.title, ex)
        if (ex is FieldValidationException) {
            return validationProblem(ex.status, request.requestURI, listOf(FieldError(ex.field, ex.reason)))
        }
        return problem(ex.status, ex.title, ex.message, request.requestURI)
    }

    @ExceptionHandler
    fun handle(ex: DataIntegrityViolationException, request: HttpServletRequest): ProblemDetail {
        val detail =
            when (generateSequence(ex as Throwable) { it.cause }.filterIsInstance<SQLException>().firstOrNull()?.sqlState) {
                UNIQUE_VIOLATION -> "A record with the same unique value already exists."
                FOREIGN_KEY_VIOLATION -> "The record is still referenced or a related record is missing."
                else -> throw ex
            }
        log.warn("Handled API exception: Conflict", ex)
        return problem(HttpStatus.CONFLICT, "Conflict", detail, request.requestURI)
    }

    /** Bean Validation failures, in FastEndpoints' `errors: [{name, reason}]` shape. */
    override fun handleMethodArgumentNotValid(
        ex: MethodArgumentNotValidException,
        headers: HttpHeaders,
        status: HttpStatusCode,
        request: WebRequest,
    ): ResponseEntity<Any>? {
        val errors = ex.bindingResult.fieldErrors.map { FieldError(it.field, it.defaultMessage ?: "is invalid") }
        return ResponseEntity.badRequest().body(validationProblem(HttpStatus.BAD_REQUEST, path(request), errors))
    }

    /** A missing required property or a value of the wrong type in the JSON body. */
    override fun handleHttpMessageNotReadable(
        ex: HttpMessageNotReadableException,
        headers: HttpHeaders,
        status: HttpStatusCode,
        request: WebRequest,
    ): ResponseEntity<Any>? {
        val cause = ex.cause as? DatabindException
        val field = cause?.path?.joinToString(".") { it.propertyName ?: "[${it.index}]" }?.replace(".[", "[")
        val errors = listOf(FieldError(field?.ifEmpty { null } ?: "body", "is missing or has the wrong type"))
        return ResponseEntity.badRequest().body(validationProblem(HttpStatus.BAD_REQUEST, path(request), errors))
    }

    data class FieldError(val name: String, val reason: String)

    private fun validationProblem(status: HttpStatus, path: String, errors: List<FieldError>) =
        problem(status, "One or more validation errors occurred.", null, path).apply {
            setProperty("errors", errors)
        }

    private fun problem(status: HttpStatusCode, title: String, detail: String?, path: String) =
        ProblemDetail.forStatus(status).apply {
            this.title = title
            this.detail = detail
            instance = URI.create(path)
        }

    private fun path(request: WebRequest) = request.getDescription(false).removePrefix("uri=")

    private companion object {
        const val UNIQUE_VIOLATION = "23505"
        const val FOREIGN_KEY_VIOLATION = "23503"
    }
}
