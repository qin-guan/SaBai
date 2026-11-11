package sabai.api.auth

import jakarta.servlet.FilterChain
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.security.core.context.SecurityContextHolder
import org.springframework.security.web.context.SecurityContextRepository
import org.springframework.transaction.support.TransactionTemplate
import org.springframework.web.filter.OncePerRequestFilter
import sabai.api.data.StaffUserRepository
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.time.ZoneOffset

/**
 * Identity's SecurityStampValidator: every few minutes, re-reads the signed-in user so
 * a password reset, lock or deleted account ends existing sessions and role changes
 * apply without signing in again. Created by [SecurityConfig], not as a bean, so it only
 * runs inside the security filter chain.
 */
class SecurityStampFilter(
    private val users: StaffUserRepository,
    private val contexts: SecurityContextRepository,
    private val transactions: TransactionTemplate,
    private val settings: AuthSettings,
    private val clock: Clock,
) : OncePerRequestFilter() {
    override fun doFilterInternal(request: HttpServletRequest, response: HttpServletResponse, chain: FilterChain) {
        val principal = SecurityContextHolder.getContext().authentication?.principal as? StaffPrincipal
        val interval = Duration.ofMinutes(settings.securityStampValidationMinutes)
        val now = Instant.now(clock)
        if (principal != null && Duration.between(principal.validatedAt, now) >= interval) {
            val user = transactions.execute { users.findById(principal.id).orElse(null) }
            if (user == null || user.securityStamp != principal.securityStamp || user.isLockedOut(now.atOffset(ZoneOffset.UTC))) {
                request.getSession(false)?.invalidate()
                SecurityContextHolder.clearContext()
            } else {
                val context = SecurityContextHolder.createEmptyContext()
                context.authentication = StaffPrincipal.of(user, now).toAuthentication()
                SecurityContextHolder.setContext(context)
                contexts.saveContext(context, request, response)
            }
        }
        chain.doFilter(request, response)
    }
}
