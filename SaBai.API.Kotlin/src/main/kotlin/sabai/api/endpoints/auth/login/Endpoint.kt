package sabai.api.endpoints.auth.login

import io.swagger.v3.oas.annotations.Operation
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import jakarta.validation.Valid
import jakarta.validation.constraints.NotBlank
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.SignInService
import sabai.api.data.AuditTrail
import sabai.api.domain.AuditAction
import sabai.api.domain.AuthRejectedException
import sabai.api.endpoints.auth.SignInResponse

/** Username (or email) and password. */
data class Request(
    /** Username, or the account's email address. */
    @NotBlank val userName: String,
    @NotBlank val password: String,
    /** Keep the session cookie after the browser closes. */
    val rememberMe: Boolean = false,
)

@RestController
class Endpoint(private val signIn: SignInService, private val audit: AuditTrail) {
    @Operation(
        summary = "Sign in with username and password",
        description =
            "Checks the password and sets the session cookie. Repeated failures lock the account for a few " +
                "minutes. When the account has two-factor on, no session is created yet: `requiresTwoFactor` " +
                "is true and the client continues with `POST /auth/login/two-factor`.",
    )
    // `noRollbackFor`: a failed attempt must still save the failed-attempt count and audit row.
    @Transactional(noRollbackFor = [Exception::class])
    @PostMapping("/auth/login")
    fun handle(@Valid @RequestBody req: Request, request: HttpServletRequest, response: HttpServletResponse): SignInResponse {
        val name = req.userName.trim()
        val user = signIn.findByNameOrEmail(name)
        if (user == null) {
            audit.record(AuditAction.SignInFailed, userName = name, details = "Password: unknown user")
            throw AuthRejectedException()
        }
        val result = signIn.passwordSignIn(user, req.password, req.rememberMe, request, response)
        return SignInResponse.from(result, audit, "Password")
    }
}
