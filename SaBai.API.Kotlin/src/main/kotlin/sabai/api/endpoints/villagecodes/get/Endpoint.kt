package sabai.api.endpoints.villagecodes.get

import io.swagger.v3.oas.annotations.Operation
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.VillageCodeRepository
import sabai.api.data.findOrNotFound
import sabai.api.endpoints.villagecodes.VillageCodeResponse
import sabai.api.endpoints.villagecodes.toResponse

@RestController
class Endpoint(private val villageCodes: VillageCodeRepository) {
    @Operation(summary = "Get a village code")
    @StaffOnly
    @Transactional(readOnly = true)
    @GetMapping("/village-codes/{id}")
    fun handle(@PathVariable id: Int): VillageCodeResponse =
        villageCodes.findOrNotFound(id, "Village code was not found.").toResponse()
}
