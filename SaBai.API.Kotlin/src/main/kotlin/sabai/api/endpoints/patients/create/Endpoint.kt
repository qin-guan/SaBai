package sabai.api.endpoints.patients.create

import io.swagger.v3.oas.annotations.Operation
import jakarta.validation.Valid
import jakarta.validation.constraints.NotBlank
import jakarta.validation.constraints.NotNull
import jakarta.validation.constraints.Positive
import jakarta.validation.constraints.Size
import org.springframework.http.ResponseEntity
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.PatientRepository
import sabai.api.data.VillageCodeRepository
import sabai.api.data.findOrNotFound
import sabai.api.domain.Gender
import sabai.api.domain.Patient
import sabai.api.domain.Visit
import sabai.api.domain.Vitals
import sabai.api.web.created
import sabai.api.web.trimToNull
import java.math.BigDecimal
import java.time.Clock
import java.time.OffsetDateTime

/** Registration payload. A visit is opened for the patient in the same transaction. */
data class Request(
    @NotBlank @Size(max = 255) val name: String,
    @Size(max = 255) val identificationNumber: String? = null,
    @Size(max = 255) val contactNo: String? = null,
    @NotNull val gender: Gender?,
    @NotNull val dateOfBirth: OffsetDateTime?,
    @NotBlank val drugAllergy: String,
    val hasPoorCard: Boolean = false,
    val hasBS2Card: Boolean = false,
    val hasSabaiCard: Boolean = false,
    val toGetReport: Boolean = false,
    @Positive val villageCodeId: Int,
    @Valid val vitals: VitalsRequest? = null,
)

/** Optional vitals captured during registration. */
data class VitalsRequest(
    val temperature: BigDecimal? = null,
    val height: BigDecimal? = null,
    val weight: BigDecimal? = null,
    val systolic: Int? = null,
    val diastolic: Int? = null,
    val heartRate: Int? = null,
    val hemocueCount: BigDecimal? = null,
    val diabetesMellitus: Boolean? = null,
    val urineTest: String? = null,
    val bloodGlucoseNonFasting: BigDecimal? = null,
    val bloodGlucoseFasting: BigDecimal? = null,
    val hba1c: BigDecimal? = null,
    val others: String? = null,
)

/** Newly registered patient, the visit opened for them, and vitals when captured. */
data class Response(
    val id: Int,
    val name: String,
    val identificationNumber: String?,
    val contactNo: String?,
    val gender: Gender,
    val dateOfBirth: OffsetDateTime,
    val drugAllergy: String,
    val hasPoorCard: Boolean,
    val hasBS2Card: Boolean,
    val hasSabaiCard: Boolean,
    val toGetReport: Boolean,
    val villageCodeId: Int?,
    val villageCode: String?,
    val displayId: String,
    val filterString: String,
    val lastVisitDate: OffsetDateTime,
    val lastVisitId: Int,
    val visitId: Int,
    val vitalsId: Int?,
    val hasPhoto: Boolean,
    val hasFace: Boolean,
)

@RestController
class Endpoint(
    private val patients: PatientRepository,
    private val villageCodes: VillageCodeRepository,
    private val clock: Clock,
) {
    @Operation(summary = "Register a patient and open a visit")
    @StaffOnly
    @Transactional
    @PostMapping("/patients")
    fun handle(@Valid @RequestBody req: Request): ResponseEntity<Response> {
        val village = villageCodes.findOrNotFound(req.villageCodeId, "Village code was not found.")
        val now = OffsetDateTime.now(clock)
        // gender/dateOfBirth are nullable only so a missing value is a validation error,
        // not a JSON error; @NotNull has run by now, so `!!` cannot throw.
        val patient =
            Patient(
                name = req.name.trim(),
                gender = req.gender!!,
                dateOfBirth = req.dateOfBirth!!,
                drugAllergy = req.drugAllergy.trim(),
                villageCode = village,
                createdAt = now,
            ).apply {
                identificationNumber = req.identificationNumber.trimToNull()
                contactNo = req.contactNo.trimToNull()
                hasPoorCard = req.hasPoorCard
                hasBS2Card = req.hasBS2Card
                hasSabaiCard = req.hasSabaiCard
                toGetReport = req.toGetReport
            }
        val visit = Visit(patient = patient, villageCode = village, date = now)
        visit.vitals = req.vitals?.toVitals(visit)
        patient.visits += visit
        patients.save(patient) // cascades to the visit and its vitals

        return created(
            "/patients/${patient.id}",
            Response(
                id = patient.id,
                name = patient.name,
                identificationNumber = patient.identificationNumber,
                contactNo = patient.contactNo,
                gender = patient.gender,
                dateOfBirth = patient.dateOfBirth,
                drugAllergy = patient.drugAllergy,
                hasPoorCard = patient.hasPoorCard,
                hasBS2Card = patient.hasBS2Card,
                hasSabaiCard = patient.hasSabaiCard,
                toGetReport = patient.toGetReport,
                villageCodeId = village.id,
                villageCode = village.code,
                displayId = Patient.formatDisplayId(village.code, patient.id),
                filterString = Patient.formatFilterString(village.code, patient.id, patient.contactNo, patient.name),
                lastVisitDate = visit.date,
                lastVisitId = visit.id,
                visitId = visit.id,
                vitalsId = visit.vitals?.id,
                hasPhoto = false,
                hasFace = false,
            ),
        )
    }

    private fun VitalsRequest.toVitals(visit: Visit) =
        Vitals(visit).also {
            it.temperature = temperature
            it.height = height
            it.weight = weight
            it.systolic = systolic
            it.diastolic = diastolic
            it.heartRate = heartRate
            it.hemocueCount = hemocueCount
            it.diabetesMellitus = diabetesMellitus
            it.urineTest = urineTest.trimToNull()
            it.bloodGlucoseNonFasting = bloodGlucoseNonFasting
            it.bloodGlucoseFasting = bloodGlucoseFasting
            it.hba1c = hba1c
            it.others = others.trimToNull()
        }
}
