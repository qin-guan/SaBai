package sabai.api.data

import org.springframework.jdbc.core.ConnectionCallback
import org.springframework.jdbc.core.JdbcTemplate
import org.springframework.stereotype.Component
import sabai.api.domain.AuditAction
import sabai.api.domain.AuditLogEntry
import sabai.api.domain.StaffUser
import java.time.Clock
import java.time.OffsetDateTime

/** Records audit events that are not row changes, such as sign-ins. */
@Component
class AuditTrail(
    private val jdbc: JdbcTemplate,
    private val writer: AuditLogWriter,
    private val clock: Clock,
) {
    /**
     * Saves one event. [user] is the account it concerns; when null the signed-in user
     * is used, or [userName] alone (an attempted name that matched no account).
     */
    fun record(action: AuditAction, user: StaffUser? = null, userName: String? = null, details: String? = null) {
        val actor = AuditActor.current()
        val entry =
            AuditLogEntry(
                occurredAt = OffsetDateTime.now(clock),
                action = action,
                userId = user?.id ?: actor.userId,
                userName = (user?.userName ?: userName ?: actor.userName)?.take(MAX_USER_NAME_LENGTH),
                details = details,
                ipAddress = actor.ipAddress,
                request = actor.request,
            )
        // Runs on the current transaction's connection when there is one.
        jdbc.execute(ConnectionCallback { connection -> writer.insert(connection, listOf(entry)) })
    }

    private companion object {
        const val MAX_USER_NAME_LENGTH = 150
    }
}
