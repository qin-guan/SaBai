package sabai.api.endpoints.patients

import org.springframework.stereotype.Component
import sabai.api.data.PatientRepository
import sabai.api.data.VisitRepository
import sabai.api.domain.Gender
import sabai.api.domain.NotFoundException
import sabai.api.domain.Patient
import sabai.api.domain.VillageCode
import sabai.api.domain.Visit
import java.time.OffsetDateTime

/** A patient, including display id and latest visit (Get, List and Update share this shape). */
data class PatientResponse(
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
    val lastVisitDate: OffsetDateTime?,
    val lastVisitId: Int?,
    val hasPhoto: Boolean,
    val hasFace: Boolean,
)

/** Columns read for a patient row: the anonymous `select new { ... }` in the C# queries. */
data class PatientRow(
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
    val photoStoredFileName: String?,
    val latestFaceId: String?,
)

data class VisitStamp(val id: Int, val patientId: Int, val date: OffsetDateTime)

/** The patient read queries, shared by the endpoints that return [PatientResponse]. */
@Component
class PatientReader(private val patients: PatientRepository, private val visits: VisitRepository) {
    fun list(name: String? = null, villageCode: String? = null, id: Int? = null): List<PatientResponse> {
        // Kotlin JDSL: a typed JPQL builder. `whereAnd` skips null conditions, so optional
        // filters need no if/else, like conditionally chaining .Where() in LINQ.
        val rows =
            patients.findAll {
                selectNew<PatientRow>(
                    path(Patient::id),
                    path(Patient::name),
                    path(Patient::identificationNumber),
                    path(Patient::contactNo),
                    path(Patient::gender),
                    path(Patient::dateOfBirth),
                    path(Patient::drugAllergy),
                    path(Patient::hasPoorCard),
                    path(Patient::hasBS2Card),
                    path(Patient::hasSabaiCard),
                    path(Patient::toGetReport),
                    path(VillageCode::id),
                    path(VillageCode::code),
                    path(Patient::photoStoredFileName),
                    path(Patient::latestFaceId),
                ).from(
                    entity(Patient::class),
                    leftJoin(Patient::villageCode),
                ).whereAnd(
                    id?.let { path(Patient::id).eq(it) },
                    name?.let { lower(path(Patient::name)).like("%${it.lowercase()}%") },
                    villageCode?.let { lower(path(VillageCode::code)).eq(it.lowercase()) },
                ).orderBy(
                    path(Patient::id).asc(),
                )
            }.filterNotNull()

        val latest = latestVisits(rows.map { it.id })
        return rows.map { row -> row.toResponse(latest[row.id]) }
    }

    fun get(id: Int): PatientResponse = list(id = id).firstOrNull() ?: throw NotFoundException("Patient was not found.")

    private fun latestVisits(patientIds: List<Int>): Map<Int, VisitStamp> {
        if (patientIds.isEmpty()) return emptyMap()
        return visits.findAll {
            selectNew<VisitStamp>(path(Visit::id), path(Visit::patient).path(Patient::id), path(Visit::date))
                .from(entity(Visit::class))
                .where(path(Visit::patient).path(Patient::id).`in`(patientIds))
        }.filterNotNull()
            .groupBy { it.patientId }
            .mapValues { (_, stamps) -> stamps.maxWith(compareBy({ it.date }, { it.id })) }
    }

    private fun PatientRow.toResponse(last: VisitStamp?) =
        PatientResponse(
            id = id,
            name = name,
            identificationNumber = identificationNumber,
            contactNo = contactNo,
            gender = gender,
            dateOfBirth = dateOfBirth,
            drugAllergy = drugAllergy,
            hasPoorCard = hasPoorCard,
            hasBS2Card = hasBS2Card,
            hasSabaiCard = hasSabaiCard,
            toGetReport = toGetReport,
            villageCodeId = villageCodeId,
            villageCode = villageCode,
            displayId = Patient.formatDisplayId(villageCode, id),
            filterString = Patient.formatFilterString(villageCode, id, contactNo, name),
            lastVisitDate = last?.date,
            lastVisitId = last?.id,
            hasPhoto = photoStoredFileName != null,
            hasFace = latestFaceId != null,
        )
}
