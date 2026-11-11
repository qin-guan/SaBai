package sabai.api.endpoints.patients.faces.enroll

import io.swagger.v3.oas.annotations.Operation
import org.springframework.http.HttpStatus
import org.springframework.http.MediaType
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestPart
import org.springframework.web.bind.annotation.ResponseStatus
import org.springframework.web.bind.annotation.RestController
import org.springframework.web.multipart.MultipartFile
import sabai.api.auth.StaffOnly
import sabai.api.data.PatientRepository
import sabai.api.data.findOrNotFound
import sabai.api.domain.FieldValidationException
import sabai.api.endpoints.patients.PatientFaceIdentity
import sabai.api.facerecognition.DetectedFace
import sabai.api.facerecognition.FaceAnalyzer
import sabai.api.facerecognition.FaceImages
import sabai.api.facerecognition.FaceStore
import java.util.UUID

data class Box(val x: Float, val y: Float, val width: Float, val height: Float)

data class Point(val x: Float, val y: Float)

data class Face(val box: Box, val score: Float, val landmarks: List<Point>) {
    companion object {
        fun from(face: DetectedFace) =
            Face(
                Box(face.box.x1, face.box.y1, face.box.width, face.box.height),
                face.score,
                face.landmarks.map { Point(it.x, it.y) },
            )
    }
}

/** A face newly enrolled for a patient. */
data class Response(val faceId: UUID, val patientId: Int, val face: Face, val facesDetected: Int)

@RestController
class Endpoint(private val patients: PatientRepository, private val analyzer: FaceAnalyzer, private val store: FaceStore) {
    @Operation(
        summary = "Enroll a face for a patient.",
        description = "Uses the most prominent face in the image. Enroll several photos per patient for better accuracy.",
    )
    @StaffOnly
    @Transactional
    @PostMapping("/patients/{id}/faces", consumes = [MediaType.MULTIPART_FORM_DATA_VALUE])
    @ResponseStatus(HttpStatus.CREATED)
    fun handle(@PathVariable id: Int, @RequestPart("image", required = false) image: MultipartFile?): Response {
        val patient = patients.findOrNotFound(id, "Patient was not found.")
        val faces = analyzer.analyze(FaceImages.load(image, "image"))
        val face =
            FaceImages.largest(faces)
                ?: throw FieldValidationException(
                    "image",
                    "No usable face detected. Use a closer, sharp, well-lit photo.",
                    HttpStatus.UNPROCESSABLE_CONTENT,
                )

        val stored = store.add(PatientFaceIdentity.forPatient(patient.id), face.embedding)
        patient.latestFaceId = stored.faceId.toString()
        return Response(stored.faceId, patient.id, Face.from(face.detection), faces.size)
    }
}
