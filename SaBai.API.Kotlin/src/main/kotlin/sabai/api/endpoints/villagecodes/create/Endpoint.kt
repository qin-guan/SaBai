package sabai.api.endpoints.villagecodes.create

import io.swagger.v3.oas.annotations.Operation
import jakarta.validation.Valid
import jakarta.validation.constraints.NotBlank
import jakarta.validation.constraints.Pattern
import jakarta.validation.constraints.Size
import org.springframework.http.ResponseEntity
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.VillageCodeRepository
import sabai.api.domain.ConflictException
import sabai.api.domain.VillageCode
import sabai.api.endpoints.villagecodes.COLOR_HEX_PATTERN
import sabai.api.endpoints.villagecodes.VillageCodeResponse
import sabai.api.endpoints.villagecodes.toResponse
import sabai.api.web.created
import java.time.Clock
import java.time.OffsetDateTime

/** Payload for creating a village code. The annotations are the FluentValidation rules. */
data class Request(
    @NotBlank @Size(max = 255) val code: String,
    @NotBlank @Size(max = 255) val name: String,
    @NotBlank @Pattern(regexp = COLOR_HEX_PATTERN) val colorHex: String,
    val isVisible: Boolean = true,
)

@RestController
class Endpoint(private val villageCodes: VillageCodeRepository, private val clock: Clock) {
    @Operation(summary = "Create a village code")
    @StaffOnly
    @Transactional
    @PostMapping("/village-codes")
    fun handle(@Valid @RequestBody req: Request): ResponseEntity<VillageCodeResponse> {
        val code = req.code.trim()
        if (villageCodes.existsByCode(code)) throw ConflictException("A village code with this code already exists.")

        val now = OffsetDateTime.now(clock)
        val village =
            villageCodes.save(
                VillageCode(
                    code = code,
                    name = req.name.trim(),
                    colorHex = req.colorHex.trim(),
                    isVisible = req.isVisible,
                    createdAt = now,
                    updatedAt = now,
                ),
            )
        return created("/village-codes/${village.id}", village.toResponse())
    }
}
