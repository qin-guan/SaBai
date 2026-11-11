package sabai.api.endpoints.broadcasts.create

import io.swagger.v3.oas.annotations.Operation
import jakarta.validation.Valid
import jakarta.validation.constraints.NotBlank
import jakarta.validation.constraints.Size
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.auth.StaffPrincipal
import sabai.api.data.BroadcastRepository
import sabai.api.domain.Broadcast
import sabai.api.domain.BroadcastSeverity
import sabai.api.endpoints.broadcasts.BroadcastResponse
import sabai.api.endpoints.broadcasts.toResponse
import sabai.api.web.created
import java.time.Clock
import java.time.OffsetDateTime

/** New unpublished broadcast. Severity defaults to info. */
data class Request(
    @NotBlank @Size(max = 255) val title: String,
    @NotBlank val body: String,
    val severity: BroadcastSeverity = BroadcastSeverity.Info,
)

@RestController
class Endpoint(private val broadcasts: BroadcastRepository, private val clock: Clock) {
    @Operation(summary = "Create a broadcast")
    @StaffOnly
    @Transactional
    @PostMapping("/broadcasts")
    fun handle(@Valid @RequestBody req: Request, @AuthenticationPrincipal user: StaffPrincipal): ResponseEntity<BroadcastResponse> {
        val broadcast =
            broadcasts.save(
                Broadcast(
                    title = req.title.trim(),
                    body = req.body.trim(),
                    severity = req.severity,
                    createdBy = user.emailOrUserName,
                    createdAt = OffsetDateTime.now(clock),
                ),
            )
        return created("/broadcasts", broadcast.toResponse())
    }
}
