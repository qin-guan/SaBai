package sabai.api.auth

import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.security.core.context.SecurityContextHolder
import org.springframework.security.web.context.SecurityContextRepository
import org.springframework.session.security.web.authentication.SpringSessionRememberMeServices
import org.springframework.stereotype.Service
import sabai.api.data.StaffUserRepository
import sabai.api.data.UserTokenRepository
import sabai.api.domain.StaffUser
import sabai.api.domain.UserToken
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.OffsetDateTime
import java.util.UUID

/** Outcome of a sign-in step. A sealed type, so callers must handle every case. */
sealed interface SignInResult {
    val user: StaffUser

    data class Succeeded(override val user: StaffUser) : SignInResult
    data class RequiresTwoFactor(override val user: StaffUser) : SignInResult
    data class LockedOut(override val user: StaffUser) : SignInResult
    data class Failed(override val user: StaffUser) : SignInResult
}

/**
 * The parts of Identity's SignInManager/UserManager the API uses: password and
 * authenticator checks with lockout, and writing the session.
 */
@Service
class SignInService(
    private val users: StaffUserRepository,
    private val tokens: UserTokenRepository,
    private val contexts: SecurityContextRepository,
    private val settings: AuthSettings,
    private val clock: Clock,
) {
    fun findByNameOrEmail(name: String): StaffUser? =
        users.findByNormalizedUserName(StaffUser.normalize(name))
            ?: if ('@' in name) users.findByNormalizedEmail(StaffUser.normalize(name)) else null

    /** `PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true)`. */
    fun passwordSignIn(
        user: StaffUser,
        password: String,
        rememberMe: Boolean,
        request: HttpServletRequest,
        response: HttpServletResponse,
    ): SignInResult {
        if (user.isLockedOut(now())) return SignInResult.LockedOut(user)
        if (user.passwordHash == null || !IdentityPasswordHasher.matches(password, user.passwordHash)) {
            return accessFailed(user)
        }
        if (IdentityPasswordHasher.upgradeEncoding(user.passwordHash)) {
            user.passwordHash = IdentityPasswordHasher.encode(password)
        }
        user.accessFailedCount = 0

        if (user.twoFactorEnabled && authenticatorKey(user) != null) {
            request.getSession(true).apply {
                setAttribute(TWO_FACTOR_USER, PendingTwoFactor(user.id, rememberMe, Instant.now(clock)))
            }
            return SignInResult.RequiresTwoFactor(user)
        }

        signIn(user, rememberMe, request, response)
        return SignInResult.Succeeded(user)
    }

    /** The user who passed the password step and still owes an authenticator code. */
    fun pendingTwoFactorUser(request: HttpServletRequest): Pair<StaffUser, Boolean>? {
        val pending = request.getSession(false)?.getAttribute(TWO_FACTOR_USER) as? PendingTwoFactor ?: return null
        if (Duration.between(pending.startedAt, Instant.now(clock)) > TWO_FACTOR_WINDOW) return null
        val user = users.findById(pending.userId).orElse(null) ?: return null
        return user to pending.rememberMe
    }

    /** `TwoFactorAuthenticatorSignInAsync(code, rememberMe, ...)`. */
    fun twoFactorSignIn(
        user: StaffUser,
        code: String,
        rememberMe: Boolean,
        request: HttpServletRequest,
        response: HttpServletResponse,
    ): SignInResult {
        if (user.isLockedOut(now())) return SignInResult.LockedOut(user)
        val key = authenticatorKey(user) ?: return SignInResult.Failed(user)
        if (!Totp.verify(key, code, Instant.now(clock))) return accessFailed(user)

        user.accessFailedCount = 0
        request.getSession(false)?.removeAttribute(TWO_FACTOR_USER)
        signIn(user, rememberMe, request, response)
        return SignInResult.Succeeded(user)
    }

    fun signOut(request: HttpServletRequest) {
        request.getSession(false)?.invalidate()
        SecurityContextHolder.clearContext()
    }

    private fun signIn(user: StaffUser, rememberMe: Boolean, request: HttpServletRequest, response: HttpServletResponse) {
        // New session id on sign-in, so a planted pre-login session cannot be reused.
        request.getSession(true)
        request.changeSessionId()
        if (rememberMe) {
            // Makes the session cookie persistent instead of ending with the browser.
            request.setAttribute(SpringSessionRememberMeServices.REMEMBER_ME_LOGIN_ATTR, true)
        }
        val context = SecurityContextHolder.createEmptyContext()
        context.authentication = StaffPrincipal.of(user, Instant.now(clock)).toAuthentication()
        SecurityContextHolder.setContext(context)
        contexts.saveContext(context, request, response)
    }

    /** `AccessFailedAsync`: count the failure and lock the account after too many. */
    private fun accessFailed(user: StaffUser): SignInResult {
        if (!user.lockoutEnabled) return SignInResult.Failed(user)
        user.accessFailedCount += 1
        if (user.accessFailedCount >= settings.maxFailedAccessAttempts) {
            user.lockoutEnd = now().plusMinutes(settings.lockoutMinutes)
            user.accessFailedCount = 0
            return SignInResult.LockedOut(user)
        }
        return SignInResult.Failed(user)
    }

    private fun authenticatorKey(user: StaffUser): String? =
        tokens.findByUserIdAndLoginProviderAndName(user.id, UserToken.IDENTITY_STORE, UserToken.AUTHENTICATOR_KEY)?.value

    private fun now() = OffsetDateTime.now(clock)

    private data class PendingTwoFactor(val userId: UUID, val rememberMe: Boolean, val startedAt: Instant) :
        java.io.Serializable

    private companion object {
        const val TWO_FACTOR_USER = "sabai.twoFactorUser"
        val TWO_FACTOR_WINDOW: Duration = Duration.ofMinutes(5)
    }
}
