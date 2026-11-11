package sabai.api.endpoints.broadcasts.list

import io.swagger.v3.oas.annotations.Operation
import jakarta.validation.Valid
import jakarta.validation.constraints.Max
import jakarta.validation.constraints.Min
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.BroadcastRepository
import sabai.api.domain.Broadcast
import sabai.api.endpoints.broadcasts.BroadcastResponse
import sabai.api.endpoints.broadcasts.toResponse

/** Paging from the query string. Limit defaults to 50 (max 200); since returns only ids above it. */
data class Request(
    @Min(1) @Max(MAX_LIMIT.toLong()) val limit: Int? = null,
    val since: Int? = null,
) {
    companion object {
        const val DEFAULT_LIMIT = 50
        const val MAX_LIMIT = 200
    }
}

@RestController
class Endpoint(private val broadcasts: BroadcastRepository) {
    @Operation(summary = "List broadcasts, newest first")
    @StaffOnly
    @Transactional(readOnly = true)
    @GetMapping("/broadcasts")
    fun handle(@Valid req: Request): List<BroadcastResponse> =
        broadcasts.findAll(offset = 0, limit = req.limit ?: Request.DEFAULT_LIMIT) {
            select(entity(Broadcast::class))
                .from(entity(Broadcast::class))
                .whereAnd(req.since?.let { path(Broadcast::id).gt(it) })
                .orderBy(path(Broadcast::id).desc())
        }.filterNotNull().map { it.toResponse() }
}
