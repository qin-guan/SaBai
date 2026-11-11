package sabai.api.endpoints.auth.whoami

import io.swagger.v3.oas.annotations.Operation
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.auth.StaffPrincipal
import sabai.api.data.StaffUserRepository
import sabai.api.domain.AuthRejectedException
import sabai.api.endpoints.auth.UserResponse

/** The staff user represented by the current session cookie. */
data class Response(val user: UserResponse)

@RestController
class Endpoint(private val users: StaffUserRepository) {
    @Operation(summary = "Return the signed-in staff user")
    @StaffOnly
    @Transactional(readOnly = true)
    @GetMapping("/auth/whoami")
    fun handle(@AuthenticationPrincipal principal: StaffPrincipal): Response {
        // 401 if the cookie points at a user that is gone.
        val user = users.findById(principal.id).orElseThrow { AuthRejectedException() }
        return Response(UserResponse.from(user))
    }
}
