package sabai.api.endpoints.broadcasts.publish

import io.swagger.v3.oas.annotations.Operation
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.BroadcastRepository
import sabai.api.data.findOrNotFound
import sabai.api.endpoints.broadcasts.BroadcastResponse
import sabai.api.endpoints.broadcasts.toResponse
import java.time.Clock
import java.time.OffsetDateTime

@RestController
class Endpoint(private val broadcasts: BroadcastRepository, private val clock: Clock) {
    @Operation(summary = "Publish a broadcast")
    @StaffOnly
    @Transactional
    @PostMapping("/broadcasts/{id}/publish")
    fun handle(@PathVariable id: Int): BroadcastResponse {
        val broadcast = broadcasts.findOrNotFound(id, "Broadcast was not found.")
        // Published once; repeating the call is a no-op.
        if (broadcast.publishedAt == null) broadcast.publishedAt = OffsetDateTime.now(clock)
        return broadcast.toResponse()
    }
}
