package sabai.api.facerecognition

import kotlin.math.sqrt

/** Resolution and geometry guards applied before generating identity embeddings. */
object FaceQuality {
    fun isSuitable(face: DetectedFace, options: FaceRecognitionOptions): Boolean {
        val box = face.box
        val finite = listOf(face.score, box.x1, box.y1, box.x2, box.y2).all { it.isFinite() }
        if (!finite || box.width <= 0 || box.height <= 0 ||
            box.width < options.minimumFaceSize || box.height < options.minimumFaceSize
        ) {
            return false
        }
        if (face.landmarks.size != 5 || face.landmarks.any { !it.x.isFinite() || !it.y.isFinite() }) return false

        val dx = face.landmarks[1].x.toDouble() - face.landmarks[0].x
        val dy = face.landmarks[1].y.toDouble() - face.landmarks[0].y
        val eyeDistance = sqrt(dx * dx + dy * dy)
        if (eyeDistance <= 1e-5 || eyeDistance < options.minimumEyeDistance) return false

        // Distinct eyes alone do not guarantee an invertible alignment transform.
        val transform = SimilarityTransform.estimate(face.landmarks, FaceAligner.ARC_FACE_TEMPLATE)
        return transform.scale.isFinite() && transform.scale > 1e-6
    }
}
