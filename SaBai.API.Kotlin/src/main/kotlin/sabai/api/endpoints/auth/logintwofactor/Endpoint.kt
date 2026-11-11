package sabai.api.endpoints.auth.logintwofactor

import io.swagger.v3.oas.annotations.Operation
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import jakarta.validation.Valid
import jakarta.validation.constraints.NotBlank
import jakarta.validation.constraints.Size
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.SignInService
import sabai.api.data.AuditTrail
import sabai.api.domain.AuthRejectedException
import sabai.api.endpoints.auth.SignInResponse

/** Second sign-in step: the 6-digit code from the authenticator app. */
data class Request(
    @NotBlank @Size(max = 16) val code: String,
    /** Ignored: the choice made on the password step applies. Kept for API compatibility. */
    val rememberMe: Boolean = false,
    /** Not ported yet (Identity's "remember this browser" cookie). */
    val rememberMachine: Boolean = false,
)

@RestController
class Endpoint(private val signIn: SignInService, private val audit: AuditTrail) {
    @Operation(
        summary = "Finish sign-in with an authenticator code",
        description = "Must follow a `POST /auth/login` that returned `requiresTwoFactor`. Sets the session cookie on success.",
    )
    @Transactional(noRollbackFor = [Exception::class])
    @PostMapping("/auth/login/two-factor")
    fun handle(@Valid @RequestBody req: Request, request: HttpServletRequest, response: HttpServletResponse): SignInResponse {
        val (user, rememberMe) = signIn.pendingTwoFactorUser(request) ?: throw AuthRejectedException()
        val code = req.code.replace(" ", "").replace("-", "")
        return SignInResponse.from(signIn.twoFactorSignIn(user, code, rememberMe, request, response), audit, "Authenticator code")
    }
}
