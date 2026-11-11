package sabai.api.endpoints.consults.get

import io.swagger.v3.oas.annotations.Operation
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.ConsultRepository
import sabai.api.domain.NotFoundException
import sabai.api.endpoints.consults.ConsultResponse
import sabai.api.endpoints.consults.toResponse

@RestController
class Endpoint(private val consults: ConsultRepository) {
    @Operation(summary = "Get a consult")
    @StaffOnly
    @Transactional(readOnly = true)
    @GetMapping("/consults/{id}")
    fun handle(@PathVariable id: Int): ConsultResponse =
        consults.findWithDoctorAndDiagnosesById(id)?.toResponse() ?: throw NotFoundException("Consult was not found.")
}
