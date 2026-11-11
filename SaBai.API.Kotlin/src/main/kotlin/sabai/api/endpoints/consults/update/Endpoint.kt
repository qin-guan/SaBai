package sabai.api.endpoints.consults.update

import io.swagger.v3.oas.annotations.Operation
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PatchMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.ConsultRepository
import sabai.api.domain.LockedException
import sabai.api.domain.NotFoundException
import sabai.api.endpoints.consults.ConsultResponse
import sabai.api.endpoints.consults.toResponse
import java.time.Clock
import java.time.OffsetDateTime

/** Consult text to change. Omitted fields are left unchanged. */
data class Request(
    val pastMedicalHistory: String? = null,
    val consultation: String? = null,
    val treatmentPlan: String? = null,
    val referredFor: String? = null,
    val referralNotes: String? = null,
    val remarks: String? = null,
)

@RestController
class Endpoint(private val consults: ConsultRepository, private val clock: Clock) {
    @Operation(summary = "Update consult text within 24 hours")
    @StaffOnly
    @Transactional
    @PatchMapping("/consults/{id}")
    fun handle(@PathVariable id: Int, @RequestBody req: Request): ConsultResponse {
        val consult = consults.findWithDoctorAndDiagnosesById(id) ?: throw NotFoundException("Consult was not found.")
        if (consult.isEditLocked(OffsetDateTime.now(clock))) throw LockedException("Cannot edit consult after 24 hours")

        req.pastMedicalHistory?.let { consult.pastMedicalHistory = it }
        req.consultation?.let { consult.consultation = it }
        req.treatmentPlan?.let { consult.treatmentPlan = it }
        req.referredFor?.let { consult.referredFor = it }
        req.referralNotes?.let { consult.referralNotes = it }
        req.remarks?.let { consult.remarks = it }
        return consult.toResponse()
    }
}
