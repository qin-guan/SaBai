package sabai.api.domain

import com.fasterxml.jackson.annotation.JsonProperty
import java.time.OffsetDateTime
import java.util.UUID

/**
 * One audited event. Rows are append-only, written with plain JDBC by
 * [sabai.api.data.AuditLogWriter], so this is a value type rather than an entity.
 */
data class AuditLogEntry(
    val occurredAt: OffsetDateTime,
    val action: AuditAction,
    val userId: UUID?,
    val userName: String?,
    val entityType: String? = null,
    val entityId: String? = null,
    val changes: List<AuditFieldChange> = emptyList(),
    val details: String? = null,
    val ipAddress: String?,
    val request: String?,
)

/** One column's value before and after. Property names match the jsonb EF Core writes. */
data class AuditFieldChange(
    @get:JsonProperty("Field") val field: String,
    @get:JsonProperty("Old") val old: String?,
    @get:JsonProperty("New") val new: String?,
)
