package sabai.api.endpoints.consults.create

import io.swagger.v3.oas.annotations.Operation
import jakarta.validation.Valid
import jakarta.validation.constraints.NotBlank
import jakarta.validation.constraints.NotEmpty
import jakarta.validation.constraints.Positive
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.auth.StaffPrincipal
import sabai.api.data.ConsultRepository
import sabai.api.data.MedicationBrandRepository
import sabai.api.data.StaffUserRepository
import sabai.api.data.VisitRepository
import sabai.api.data.findOrNotFound
import sabai.api.domain.BadRequestException
import sabai.api.domain.Consult
import sabai.api.domain.Diagnosis
import sabai.api.domain.DiagnosisCategory
import sabai.api.domain.Order
import sabai.api.endpoints.consults.ConsultResponse
import sabai.api.endpoints.consults.toResponse
import sabai.api.web.created
import java.time.Clock
import java.time.OffsetDateTime

/** New consult, its diagnoses, and any prescriptions to record together. */
data class Request(
    @Positive val visitId: Int,
    val pastMedicalHistory: String? = null,
    val consultation: String? = null,
    val treatmentPlan: String? = null,
    val referredFor: String? = null,
    val referralNotes: String? = null,
    val remarks: String? = null,
    @NotEmpty val diagnoses: List<@Valid DiagnosisInput> = emptyList(),
    val orders: List<@Valid OrderInput> = emptyList(),
)

/** Diagnosis captured with a new consult. */
data class DiagnosisInput(@NotBlank val details: String = "", val category: DiagnosisCategory? = null)

/** Prescription inserted with a new consult. Status is always pending. */
data class OrderInput(
    @Positive val medicationBrandId: Int,
    @Positive val quantity: Int,
    val dosageInstructions: String? = null,
    val remarks: String? = null,
)

@RestController
class Endpoint(
    private val consults: ConsultRepository,
    private val visits: VisitRepository,
    private val brands: MedicationBrandRepository,
    private val users: StaffUserRepository,
    private val clock: Clock,
) {
    @Operation(summary = "Record a consult with diagnoses and optional prescriptions")
    @StaffOnly
    @Transactional
    @PostMapping("/consults")
    fun handle(@Valid @RequestBody req: Request, @AuthenticationPrincipal user: StaffPrincipal): ResponseEntity<ConsultResponse> {
        val visit = visits.findOrNotFound(req.visitId, "Visit was not found.")
        val doctor = users.findOrNotFound(user.id, "Doctor was not found.")
        val brandIds = req.orders.map { it.medicationBrandId }.toSet()
        if (brands.countByIdIn(brandIds) != brandIds.size.toLong()) throw BadRequestException("Medication brand was not found.")

        val now = OffsetDateTime.now(clock)
        val consult =
            Consult(date = now, doctor = doctor, visit = visit).apply {
                pastMedicalHistory = req.pastMedicalHistory
                consultation = req.consultation
                treatmentPlan = req.treatmentPlan
                referredFor = req.referredFor
                referralNotes = req.referralNotes
                remarks = req.remarks
            }
        req.diagnoses.mapTo(consult.diagnoses) { Diagnosis(it.details.trim(), it.category, consult) }
        req.orders.mapTo(consult.orders) {
            Order(
                consult = consult,
                medicationBrand = brands.getReferenceById(it.medicationBrandId), // FK only, no SELECT
                dosageInstructions = it.dosageInstructions.orEmpty(),
                quantity = it.quantity,
                createdAt = now,
            ).apply { remarks = it.remarks }
        }
        consults.save(consult)
        return created("/consults/${consult.id}", consult.toResponse())
    }
}
