package sabai.api.endpoints.consults.pdf

import io.swagger.v3.oas.annotations.Operation
import org.springframework.http.ContentDisposition
import org.springframework.http.MediaType
import org.springframework.http.ResponseEntity
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.ConsultRepository
import sabai.api.domain.NotFoundException
import sabai.api.domain.Patient

@RestController
class Endpoint(private val consults: ConsultRepository) {
    @Operation(summary = "Download a consult as a PDF")
    @StaffOnly
    @Transactional(readOnly = true)
    @GetMapping("/consults/{id}/pdf")
    fun handle(@PathVariable id: Int): ResponseEntity<ByteArray> {
        val consult = consults.findForReportById(id) ?: throw NotFoundException("Consult was not found.")
        val patient = consult.visit.patient
        val code = patient.villageCode?.code ?: consult.visit.villageCode.code
        val displayId = Patient.formatDisplayId(code, patient.id)

        val bytes =
            ConsultPdf.create(
                ConsultPdfContent(
                    patientDisplayId = displayId,
                    patientName = patient.name,
                    doctorNickname = consult.doctor.nickname,
                    pastMedicalHistory = consult.pastMedicalHistory,
                    consultation = consult.consultation,
                    diagnoses = consult.diagnoses.sortedBy { it.id }.map { ConsultPdfDiagnosis(it.category?.label.orEmpty(), it.details) },
                    treatmentPlan = consult.treatmentPlan,
                    referredFor = consult.referredFor,
                    referralNotes = consult.referralNotes,
                    remarks = consult.remarks,
                ),
            )

        val safeId = displayId.map { if (it.isLetterOrDigit() && it.code < 128) it else '_' }.joinToString("")
        val fileName = "consultation${consult.id}_report_$safeId.pdf"
        return ResponseEntity.ok()
            .contentType(MediaType.APPLICATION_PDF)
            .header("Content-Disposition", ContentDisposition.inline().filename(fileName).build().toString())
            .body(bytes)
    }
}
