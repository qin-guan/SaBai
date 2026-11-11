package sabai.api.data

import org.springframework.security.core.context.SecurityContextHolder
import org.springframework.web.context.request.RequestContextHolder
import org.springframework.web.context.request.ServletRequestAttributes
import sabai.api.auth.StaffPrincipal
import java.util.UUID

/** Who is acting in the current request, for audit entries. */
data class AuditActor(val userId: UUID?, val userName: String?, val ipAddress: String?, val request: String?) {
    companion object {
        fun current(): AuditActor {
            val principal = SecurityContextHolder.getContext().authentication?.principal as? StaffPrincipal
            val http = (RequestContextHolder.getRequestAttributes() as? ServletRequestAttributes)?.request
            return AuditActor(
                userId = principal?.id,
                userName = principal?.userName,
                ipAddress = http?.remoteAddr,
                request = http?.let { "${it.method} ${it.requestURI}".take(500) },
            )
        }
    }
}
