package sabai.api.endpoints.patients.searchbyface

import io.swagger.v3.oas.annotations.Operation
import org.springframework.http.MediaType
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RequestPart
import org.springframework.web.bind.annotation.RestController
import org.springframework.web.multipart.MultipartFile
import sabai.api.auth.StaffOnly
import sabai.api.data.PatientRepository
import sabai.api.domain.FieldValidationException
import sabai.api.domain.Patient
import sabai.api.domain.VillageCode
import sabai.api.endpoints.patients.PatientFaceIdentity
import sabai.api.facerecognition.FaceAnalyzer
import sabai.api.facerecognition.FaceImages
import sabai.api.facerecognition.FaceRecognitionOptions
import sabai.api.facerecognition.FaceStore

/** A patient that matched the search photo, with the best similarity seen. */
data class PatientFaceMatch(
    val id: Int,
    val name: String,
    val displayId: String,
    val villageCode: String?,
    val villageColorHex: String?,
    val hasPhoto: Boolean,
    val similarity: Float,
)

data class Response(val matches: List<PatientFaceMatch>)

data class MatchRow(val id: Int, val name: String, val photoStoredFileName: String?, val villageCode: String?, val villageColorHex: String?)

@RestController
class Endpoint(
    private val patients: PatientRepository,
    private val analyzer: FaceAnalyzer,
    private val store: FaceStore,
    private val options: FaceRecognitionOptions,
) {
    @Operation(summary = "Search for patients by face photo")
    @StaffOnly
    @Transactional(readOnly = true)
    @PostMapping("/patients/search-by-face", consumes = [MediaType.MULTIPART_FORM_DATA_VALUE])
    fun handle(
        @RequestPart("image", required = false) image: MultipartFile?,
        /** Minimum cosine similarity for a match. Defaults to the configured match threshold. */
        @RequestParam(required = false) threshold: Float?,
        /** Maximum number of candidate patients returned. */
        @RequestParam(defaultValue = "5") maxMatches: Int,
    ): Response {
        if (threshold != null && threshold !in -1f..1f) throw FieldValidationException("threshold", "Threshold must be between -1 and 1.")
        if (maxMatches !in 1..20) throw FieldValidationException("maxMatches", "MaxMatches must be between 1 and 20.")
        val faces = analyzer.analyze(FaceImages.load(image, "image"))

        // Best similarity per patient across every detected face.
        val best = mutableMapOf<Int, Float>()
        for (face in faces) {
            for (match in store.search(face.embedding, threshold ?: options.matchThreshold, maxMatches)) {
                val patientId = PatientFaceIdentity.parse(match.personId) ?: continue
                best.merge(patientId, match.similarity, ::maxOf)
            }
        }
        if (best.isEmpty()) return Response(emptyList())

        val rows =
            patients.findAll {
                selectNew<MatchRow>(
                    path(Patient::id),
                    path(Patient::name),
                    path(Patient::photoStoredFileName),
                    path(VillageCode::code),
                    path(VillageCode::colorHex),
                ).from(entity(Patient::class), leftJoin(Patient::villageCode))
                    .where(path(Patient::id).`in`(best.keys))
            }.filterNotNull()

        return Response(
            rows.map {
                PatientFaceMatch(
                    id = it.id,
                    name = it.name,
                    displayId = Patient.formatDisplayId(it.villageCode, it.id),
                    villageCode = it.villageCode,
                    villageColorHex = it.villageColorHex,
                    hasPhoto = it.photoStoredFileName != null,
                    similarity = best.getValue(it.id),
                )
            }.sortedByDescending { it.similarity }
                .take(maxMatches),
        )
    }
}
