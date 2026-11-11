package sabai.api.data

import org.springframework.stereotype.Component
import sabai.api.domain.AuditLogEntry
import tools.jackson.databind.ObjectMapper
import java.sql.Connection
import java.sql.Timestamp
import java.sql.Types

/** Inserts audit rows on whatever connection (and so transaction) it is given. */
@Component
class AuditLogWriter(private val json: ObjectMapper) {
    fun insert(connection: Connection, entries: List<AuditLogEntry>) {
        if (entries.isEmpty()) return
        connection.prepareStatement(SQL).use { statement ->
            for (entry in entries) {
                statement.setTimestamp(1, Timestamp.from(entry.occurredAt.toInstant()))
                statement.setString(2, entry.action.name)
                statement.setObject(3, entry.userId, Types.OTHER)
                statement.setString(4, entry.userName)
                statement.setString(5, entry.entityType)
                statement.setString(6, entry.entityId)
                statement.setString(7, entry.details)
                statement.setString(8, entry.ipAddress)
                statement.setString(9, entry.request)
                statement.setString(10, json.writeValueAsString(entry.changes))
                statement.addBatch()
            }
            statement.executeBatch()
        }
    }

    private companion object {
        val SQL =
            """
            INSERT INTO "AuditLogs" ("OccurredAt", "Action", "UserId", "UserName", "EntityType",
                "EntityId", "Details", "IpAddress", "Request", "Changes")
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?::jsonb)
            """.trimIndent()
    }
}
