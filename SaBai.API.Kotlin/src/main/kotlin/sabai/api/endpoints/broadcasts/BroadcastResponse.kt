package sabai.api.endpoints.broadcasts

import sabai.api.domain.Broadcast
import sabai.api.domain.BroadcastSeverity
import java.time.OffsetDateTime

/** A clinic broadcast. */
data class BroadcastResponse(
    val id: Int,
    val title: String,
    val body: String,
    val severity: BroadcastSeverity,
    val createdBy: String?,
    val createdAt: OffsetDateTime,
    val publishedAt: OffsetDateTime?,
    val attempts: Int,
)

fun Broadcast.toResponse() = BroadcastResponse(id, title, body, severity, createdBy, createdAt, publishedAt, attempts)
