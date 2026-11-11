package sabai.api.endpoints.auth.logout

import io.swagger.v3.oas.annotations.Operation
import jakarta.servlet.http.HttpServletRequest
import org.springframework.http.HttpStatus
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.ResponseStatus
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.SignInService
import sabai.api.auth.StaffPrincipal
import sabai.api.data.AuditTrail
import sabai.api.domain.AuditAction

@RestController
class Endpoint(private val signIn: SignInService, private val audit: AuditTrail) {
    @Operation(summary = "Sign out and clear the session cookie")
    @Transactional
    @PostMapping("/auth/logout")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    fun handle(@AuthenticationPrincipal user: StaffPrincipal?, request: HttpServletRequest) {
        if (user != null) audit.record(AuditAction.SignedOut)
        signIn.signOut(request)
    }
}
