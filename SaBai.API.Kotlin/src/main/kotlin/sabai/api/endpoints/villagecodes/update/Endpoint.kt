package sabai.api.endpoints.villagecodes.update

import io.swagger.v3.oas.annotations.Operation
import jakarta.validation.Valid
import jakarta.validation.constraints.Pattern
import jakarta.validation.constraints.Size
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PatchMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.VillageCodeRepository
import sabai.api.data.findOrNotFound
import sabai.api.domain.ConflictException
import sabai.api.endpoints.villagecodes.COLOR_HEX_PATTERN
import sabai.api.endpoints.villagecodes.VillageCodeResponse
import sabai.api.endpoints.villagecodes.toResponse
import sabai.api.web.NullOrNotBlank
import java.time.Clock
import java.time.OffsetDateTime

/** Partial update. Omitted (null) fields are left unchanged. */
data class Request(
    @NullOrNotBlank @Size(max = 255) val code: String? = null,
    @NullOrNotBlank @Size(max = 255) val name: String? = null,
    @Pattern(regexp = COLOR_HEX_PATTERN) val colorHex: String? = null,
    val isVisible: Boolean? = null,
)

@RestController
class Endpoint(private val villageCodes: VillageCodeRepository, private val clock: Clock) {
    @Operation(summary = "Update a village code")
    @StaffOnly
    @Transactional
    @PatchMapping("/village-codes/{id}")
    fun handle(@PathVariable id: Int, @Valid @RequestBody req: Request): VillageCodeResponse {
        val village = villageCodes.findOrNotFound(id, "Village code was not found.")

        req.code?.trim()?.let { code ->
            if (villageCodes.existsByCodeAndIdNot(code, village.id)) {
                throw ConflictException("A village code with this code already exists.")
            }
            village.code = code
        }
        req.name?.let { village.name = it.trim() }
        req.colorHex?.let { village.colorHex = it.trim() }
        req.isVisible?.let { village.isVisible = it }
        village.updatedAt = OffsetDateTime.now(clock)

        // No SaveChanges: the transaction flushes changes to loaded entities on commit.
        return village.toResponse()
    }
}
