package sabai.api.endpoints.consults

import sabai.api.domain.Consult
import sabai.api.domain.DiagnosisCategory
import java.time.OffsetDateTime
import java.util.UUID

/** Consult with its visit, doctor nickname and diagnoses (Create, Get and Update share it). */
data class ConsultResponse(
    val id: Int,
    val visitId: Int,
    val date: OffsetDateTime,
    val pastMedicalHistory: String?,
    val consultation: String?,
    val treatmentPlan: String?,
    val referredFor: String?,
    val referralNotes: String?,
    val remarks: String?,
    val doctorId: UUID,
    val doctorNickname: String,
    val diagnoses: List<DiagnosisItem>,
)

data class DiagnosisItem(val id: Int, val consultId: Int, val details: String, val category: DiagnosisCategory?)

fun Consult.toResponse() =
    ConsultResponse(
        id = id,
        visitId = visit.id,
        date = date,
        pastMedicalHistory = pastMedicalHistory,
        consultation = consultation,
        treatmentPlan = treatmentPlan,
        referredFor = referredFor,
        referralNotes = referralNotes,
        remarks = remarks,
        doctorId = doctor.id,
        doctorNickname = doctor.nickname,
        diagnoses = diagnoses.sortedBy { it.id }.map { DiagnosisItem(it.id, id, it.details, it.category) },
    )
