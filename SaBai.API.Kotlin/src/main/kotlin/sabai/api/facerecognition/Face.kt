package sabai.api.facerecognition

import kotlin.math.max
import kotlin.math.min

data class PointF(val x: Float, val y: Float)

data class BoundingBox(val x1: Float, val y1: Float, val x2: Float, val y2: Float) {
    val width: Float get() = max(0f, x2 - x1)
    val height: Float get() = max(0f, y2 - y1)
    val area: Float get() = width * height

    fun iou(other: BoundingBox): Float {
        val w = min(x2, other.x2) - max(x1, other.x1)
        val h = min(y2, other.y2) - max(y1, other.y1)
        if (w <= 0 || h <= 0) return 0f
        val intersection = w * h
        return intersection / (area + other.area - intersection)
    }
}

/**
 * A detected face: bounding box, detector confidence and the five landmarks
 * (left eye, right eye, nose tip, left mouth corner, right mouth corner).
 */
data class DetectedFace(val box: BoundingBox, val score: Float, val landmarks: List<PointF>)

/** A detected face with its L2-normalised identity embedding. */
class AnalyzedFace(val detection: DetectedFace, val embedding: FloatArray)

class InvalidImageException(message: String) : RuntimeException(message)
