package sabai.api.facerecognition

import kotlin.math.max
import kotlin.math.min

/** Bounded detection retries; recognition always samples pixels from the original photo. */
internal object BestEffortFaceDetection {
    fun detect(
        image: RgbImage,
        options: FaceRecognitionOptions,
        detect: (RgbImage, Int, Float, Float) -> List<DetectedFace>,
    ): List<DetectedFace> {
        val geometryOnly = FaceRecognitionOptions()

        // Reject invalid geometry before it can suppress usable detections across passes.
        fun run(input: RgbImage, size: Int, threshold: Float) =
            detect(input, size, threshold, options.nmsThreshold).filter { FaceQuality.isSuitable(it, geometryOnly) }

        val primary = run(image, options.detectionSize, options.detectionThreshold)
        if (!options.enableDetectionFallback ||
            (primary.isNotEmpty() && primary.all { min(it.box.width, it.box.height) >= 64 })
        ) {
            return primary
        }

        val size = max(options.detectionSize, options.detectionFallbackSize)
        // Keep existing primary landmarks rather than replacing them with a different pass.
        val merged = merge(primary, run(image, size, options.detectionFallbackThreshold), options.nmsThreshold)
        if (merged.isNotEmpty()) return merged

        val brightened = ImageOps.brightenShadows(image)
        val lighting = run(brightened, size, options.detectionFallbackThreshold)
        if (lighting.isNotEmpty()) return lighting

        // Roll retries only after unrotated passes fail. They do not fix a full profile.
        val rotatedFaces =
            listOf(-25.0, 25.0).flatMap { degrees ->
                val (rotated, transform) = ImageOps.rotate(brightened, degrees)
                val inverse = transform.inverse()
                run(rotated, size, options.detectionFallbackThreshold)
                    .map { mapToOriginal(it, inverse, image.width, image.height) }
                    .filter { FaceQuality.isSuitable(it, geometryOnly) }
            }
        return ScrfdFaceDetector.nonMaxSuppression(rotatedFaces, options.nmsThreshold)
    }

    fun merge(primary: List<DetectedFace>, retry: List<DetectedFace>, nmsThreshold: Float): List<DetectedFace> {
        val additional = retry.filter { f -> primary.all { it.box.iou(f.box) <= nmsThreshold } }
        return (primary + ScrfdFaceDetector.nonMaxSuppression(additional, nmsThreshold)).sortedByDescending { it.score }
    }

    fun mapToOriginal(face: DetectedFace, inverse: SimilarityTransform, width: Int, height: Int): DetectedFace {
        val b = face.box
        val corners =
            listOf(
                inverse.apply(b.x1.toDouble(), b.y1.toDouble()),
                inverse.apply(b.x2.toDouble(), b.y1.toDouble()),
                inverse.apply(b.x1.toDouble(), b.y2.toDouble()),
                inverse.apply(b.x2.toDouble(), b.y2.toDouble()),
            )
        return face.copy(
            box =
                BoundingBox(
                    corners.minOf { it.x }.coerceIn(0f, width.toFloat()),
                    corners.minOf { it.y }.coerceIn(0f, height.toFloat()),
                    corners.maxOf { it.x }.coerceIn(0f, width.toFloat()),
                    corners.maxOf { it.y }.coerceIn(0f, height.toFloat()),
                ),
            landmarks = face.landmarks.map { inverse.apply(it.x.toDouble(), it.y.toDouble()) },
        )
    }
}
