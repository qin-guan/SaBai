package sabai.api.endpoints.villagecodes.delete

import io.swagger.v3.oas.annotations.Operation
import org.springframework.http.HttpStatus
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.DeleteMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.ResponseStatus
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.VillageCodeRepository
import sabai.api.data.VisitRepository
import sabai.api.data.findOrNotFound
import sabai.api.domain.ConflictException

@RestController
class Endpoint(private val villageCodes: VillageCodeRepository, private val visits: VisitRepository) {
    @Operation(summary = "Delete a village code")
    @StaffOnly
    @Transactional
    @DeleteMapping("/village-codes/{id}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    fun handle(@PathVariable id: Int) {
        val village = villageCodes.findOrNotFound(id, "Village code was not found.")
        if (visits.existsByVillageCodeId(village.id)) throw ConflictException("A visit still references this village code.")
        villageCodes.delete(village)
    }
}
