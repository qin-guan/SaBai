package sabai.api.facerecognition

import kotlin.math.atan2
import kotlin.math.sqrt

/**
 * Aligns a face to the canonical 112x112 ArcFace crop with a least-squares similarity
 * transform (rotation, uniform scale, translation) of its five landmarks.
 */
object FaceAligner {
    const val SIZE = 112

    /** Reference landmark positions used to train InsightFace ArcFace models. */
    val ARC_FACE_TEMPLATE =
        listOf(
            PointF(38.2946f, 51.6963f),
            PointF(73.5318f, 51.5014f),
            PointF(56.0252f, 71.7366f),
            PointF(41.5493f, 92.3655f),
            PointF(70.7299f, 92.2041f),
        )

    fun align(image: RgbImage, landmarks: List<PointF>): RgbImage {
        val inverse = SimilarityTransform.estimate(landmarks, ARC_FACE_TEMPLATE).inverse()
        val output = RgbImage(SIZE, SIZE)
        for (y in 0 until SIZE) {
            for (x in 0 until SIZE) {
                val (sx, sy) = inverse.apply(x.toDouble(), y.toDouble())
                ImageOps.sampleBilinear(image, sx, sy, output.pixels, (y * SIZE + x) * 3)
            }
        }
        return output
    }
}

/** 2D similarity transform: [x', y'] = [[a, -b], [b, a]] * [x, y] + [tx, ty]. */
data class SimilarityTransform(val a: Double, val b: Double, val tx: Double, val ty: Double) {
    val scale: Double get() = sqrt(a * a + b * b)
    val rotationRadians: Double get() = atan2(b, a)

    fun apply(x: Double, y: Double): PointF = PointF((a * x - b * y + tx).toFloat(), (b * x + a * y + ty).toFloat())

    fun inverse(): SimilarityTransform {
        val d = a * a + b * b
        val ia = a / d
        val ib = -b / d
        return SimilarityTransform(ia, ib, -(ia * tx - ib * ty), -(ib * tx + ia * ty))
    }

    companion object {
        /** Closed-form least-squares fit (Umeyama without reflection) mapping src onto dst. */
        fun estimate(src: List<PointF>, dst: List<PointF>): SimilarityTransform {
            require(src.size == dst.size && src.size >= 2) { "Need at least two matching point pairs." }
            val msx = src.sumOf { it.x.toDouble() } / src.size
            val msy = src.sumOf { it.y.toDouble() } / src.size
            val mdx = dst.sumOf { it.x.toDouble() } / dst.size
            val mdy = dst.sumOf { it.y.toDouble() } / dst.size

            var norm = 0.0
            var dot = 0.0
            var cross = 0.0
            for ((s, d) in src.zip(dst)) {
                val ax = s.x - msx
                val ay = s.y - msy
                val bx = d.x - mdx
                val by = d.y - mdy
                norm += ax * ax + ay * ay
                dot += ax * bx + ay * by
                cross += ax * by - ay * bx
            }
            require(norm >= 1e-12) { "Source points are degenerate." }

            val a = dot / norm
            val b = cross / norm
            return SimilarityTransform(a, b, mdx - (a * msx - b * msy), mdy - (b * msx + a * msy))
        }
    }
}

