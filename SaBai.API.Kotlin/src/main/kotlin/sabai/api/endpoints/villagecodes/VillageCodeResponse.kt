package sabai.api.endpoints.villagecodes

import sabai.api.domain.VillageCode
import java.time.OffsetDateTime

/**
 * A village code. The C# API declares an identical record per endpoint; in Kotlin one
 * shared type plus a mapping extension does the same job.
 */
data class VillageCodeResponse(
    val id: Int,
    val code: String,
    val name: String,
    val colorHex: String,
    val isVisible: Boolean,
    val createdAt: OffsetDateTime,
    val updatedAt: OffsetDateTime,
)

fun VillageCode.toResponse() = VillageCodeResponse(id, code, name, colorHex, isVisible, createdAt, updatedAt)

const val COLOR_HEX_PATTERN = "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$"
