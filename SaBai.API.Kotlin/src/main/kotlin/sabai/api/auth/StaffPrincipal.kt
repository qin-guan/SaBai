package sabai.api.auth

import org.springframework.security.authentication.UsernamePasswordAuthenticationToken
import org.springframework.security.core.Authentication
import org.springframework.security.core.authority.SimpleGrantedAuthority
import sabai.api.domain.StaffUser
import java.io.Serializable
import java.security.Principal
import java.time.Instant
import java.util.UUID

/**
 * What the session remembers about the signed-in user (the claims in the C# cookie).
 * Stored in the server-side session, so it must be Serializable.
 */
data class StaffPrincipal(
    val id: UUID,
    val userName: String,
    val email: String?,
    val roles: List<String>,
    val securityStamp: String?,
    val validatedAt: Instant,
) : Principal, Serializable {
    /** Spring Session indexes sessions by this (PRINCIPAL_NAME). */
    override fun getName(): String = userName

    /** Email when the account has one, otherwise the username (`User.Email()` in C#). */
    val emailOrUserName: String get() = email?.ifBlank { null } ?: userName

    fun toAuthentication(): Authentication =
        UsernamePasswordAuthenticationToken.authenticated(this, null, roles.map(::SimpleGrantedAuthority))

    companion object {
        fun of(user: StaffUser, now: Instant) =
            StaffPrincipal(user.id, user.userName, user.email, user.roleNames(), user.securityStamp, now)
    }
}
