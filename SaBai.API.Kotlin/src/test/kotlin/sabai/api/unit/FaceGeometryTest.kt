package sabai.api.unit

import org.assertj.core.api.Assertions.assertThat
import org.assertj.core.data.Offset
import org.junit.jupiter.api.Test
import sabai.api.facerecognition.ArcFaceEmbedder
import sabai.api.facerecognition.BoundingBox
import sabai.api.facerecognition.DetectedFace
import sabai.api.facerecognition.FaceAligner
import sabai.api.facerecognition.FaceMatch
import sabai.api.facerecognition.FaceStore
import sabai.api.facerecognition.PointF
import sabai.api.facerecognition.RgbImage
import sabai.api.facerecognition.ScrfdFaceDetector
import sabai.api.facerecognition.SimilarityTransform
import java.util.UUID

class FaceGeometryTest {
    @Test
    fun `intersection over union`() {
        val a = BoundingBox(0f, 0f, 10f, 10f)
        assertThat(a.iou(a)).isEqualTo(1f)
        assertThat(a.iou(BoundingBox(5f, 0f, 15f, 10f))).isCloseTo(1f / 3, Offset.offset(1e-6f))
        assertThat(a.iou(BoundingBox(20f, 20f, 30f, 30f))).isZero()
    }

    @Test
    fun `non-max suppression keeps the strongest of overlapping boxes`() {
        fun face(x: Float, score: Float) = DetectedFace(BoundingBox(x, 0f, x + 10, 10f), score, emptyList())
        val kept = ScrfdFaceDetector.nonMaxSuppression(listOf(face(0f, 0.6f), face(1f, 0.9f), face(50f, 0.7f)), 0.4f)
        assertThat(kept.map { it.score }).containsExactly(0.9f, 0.7f)
    }

    @Test
    fun `similarity transform recovers a known rotation, scale and shift`() {
        val truth = SimilarityTransform(a = 1.2 * Math.cos(0.3), b = 1.2 * Math.sin(0.3), tx = 5.0, ty = -3.0)
        val src = FaceAligner.ARC_FACE_TEMPLATE
        val dst = src.map { truth.apply(it.x.toDouble(), it.y.toDouble()) }
        val estimated = SimilarityTransform.estimate(src, dst)
        assertThat(estimated.scale).isCloseTo(1.2, Offset.offset(1e-5))
        assertThat(estimated.rotationRadians).isCloseTo(0.3, Offset.offset(1e-5))
        val back = estimated.inverse().apply(dst[0].x.toDouble(), dst[0].y.toDouble())
        assertThat(back.x).isCloseTo(src[0].x, Offset.offset(1e-3f))
    }

    @Test
    fun `EXIF orientation 6 rotates a landscape image to portrait`() {
        val image = RgbImage(3, 2, ByteArray(18) { it.toByte() })
        val oriented = RgbImage.orient(image, 6)
        assertThat(oriented.width).isEqualTo(2)
        assertThat(oriented.height).isEqualTo(3)
        assertThat(RgbImage.orient(image, 1)).isSameAs(image)
    }

    @Test
    fun `search keeps the best match per person`() {
        fun match(person: String, similarity: Float) = FaceMatch(UUID.randomUUID(), person, similarity)
        val best = FaceStore.bestMatches(listOf(match("a", 0.5f), match("b", 0.7f), match("a", 0.9f), match("c", 0.4f)), 2)
        assertThat(best.map { it.personId to it.similarity }).containsExactly("a" to 0.9f, "b" to 0.7f)
    }

    @Test
    fun `embeddings are L2-normalised`() {
        val v = ArcFaceEmbedder.normalize(floatArrayOf(3f, 4f))
        assertThat(v).containsExactly(0.6f, 0.8f)
        assertThat(ArcFaceEmbedder.similarity(v, v)).isCloseTo(1f, Offset.offset(1e-6f))
        assertThat(PointF(1f, 2f).component1()).isEqualTo(1f)
    }
}
