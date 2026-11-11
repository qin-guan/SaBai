package sabai.api.facerecognition

import ai.onnxruntime.OnnxTensor
import ai.onnxruntime.OrtEnvironment
import ai.onnxruntime.OrtSession
import ai.onnxruntime.TensorInfo
import java.nio.FloatBuffer
import kotlin.math.max

/**
 * SCRFD face detector (InsightFace). Produces boxes and five-point landmarks from the
 * stride 8/16/32 heads of the model.
 */
class ScrfdFaceDetector(private val env: OrtEnvironment, private val session: OrtSession) : AutoCloseable {
    private val inputName = session.inputNames.single()
    private val outputNames = session.outputNames.toList()
    private val fixedInputSize: Int?

    init {
        val dims = (session.inputInfo.getValue(inputName).info as TensorInfo).shape
        if (dims.size != 4 || (dims[1] > 0 && dims[1] != 3L)) throw UnsupportedOperationException("SCRFD requires an NCHW RGB input.")
        fixedInputSize =
            if (dims[2] > 0 || dims[3] > 0) {
                if (dims[2] <= 0 || dims[2] != dims[3] || dims[2] % 32 != 0L) {
                    throw UnsupportedOperationException("SCRFD requires a square input with sides divisible by 32.")
                }
                dims[2].toInt()
            } else {
                null
            }
        // Landmark-capable 3-stride models expose 9 outputs: scores, boxes and landmarks per stride.
        if (outputNames.size != 9) {
            throw UnsupportedOperationException(
                "Unsupported SCRFD model with ${outputNames.size} outputs; a 3-stride model with landmarks is required.",
            )
        }
    }

    fun detect(image: RgbImage, requestedSize: Int, scoreThreshold: Float, nmsThreshold: Float): List<DetectedFace> {
        val inputSize = fixedInputSize ?: requestedSize
        // Letterbox: keep the aspect ratio and pad the bottom/right edge.
        val ratio = image.height.toFloat() / image.width
        val (newWidth, newHeight) =
            if (ratio > 1) max(1, (inputSize / ratio).toInt()) to inputSize else inputSize to max(1, (inputSize * ratio).toInt())
        val scaleX = newWidth.toFloat() / image.width
        val scaleY = newHeight.toFloat() / image.height
        val resized = ImageOps.resizeBilinear(image, newWidth, newHeight)

        val candidates = mutableListOf<DetectedFace>()
        OnnxTensor.createTensor(env, toTensor(resized, inputSize), longArrayOf(1, 3, inputSize.toLong(), inputSize.toLong())).use { input ->
            session.run(mapOf(inputName to input)).use { results ->
                val outputs = outputNames.map { (results.get(it).get() as OnnxTensor).floatBuffer }
                for ((level, stride) in STRIDES.withIndex()) {
                    val scores = outputs[level]
                    val boxes = outputs[level + STRIDES.size]
                    val kps = outputs[level + STRIDES.size * 2]
                    val grid = inputSize / stride
                    for (i in 0 until grid * grid * ANCHORS_PER_LOCATION) {
                        val score = scores[i]
                        if (!score.isFinite() || score < scoreThreshold) continue
                        val cell = i / ANCHORS_PER_LOCATION
                        val cx = (cell % grid * stride).toFloat()
                        val cy = (cell / grid * stride).toFloat()
                        val box =
                            BoundingBox(
                                (cx - boxes[i * 4] * stride) / scaleX,
                                (cy - boxes[i * 4 + 1] * stride) / scaleY,
                                (cx + boxes[i * 4 + 2] * stride) / scaleX,
                                (cy + boxes[i * 4 + 3] * stride) / scaleY,
                            )
                        val landmarks =
                            List(5) { k ->
                                PointF(
                                    (cx + kps[i * 10 + k * 2] * stride) / scaleX,
                                    (cy + kps[i * 10 + k * 2 + 1] * stride) / scaleY,
                                )
                            }
                        candidates += DetectedFace(box, score, landmarks)
                    }
                }
            }
        }
        return nonMaxSuppression(candidates, nmsThreshold).map { it.copy(box = clamp(it.box, image.width, image.height)) }
    }

    override fun close() = session.close()

    companion object {
        private val STRIDES = intArrayOf(8, 16, 32)
        private const val ANCHORS_PER_LOCATION = 2

        fun nonMaxSuppression(faces: Iterable<DetectedFace>, iouThreshold: Float): List<DetectedFace> {
            val kept = mutableListOf<DetectedFace>()
            for (face in faces.sortedByDescending { it.score }) {
                if (kept.all { it.box.iou(face.box) <= iouThreshold }) kept += face
            }
            return kept
        }

        private fun clamp(b: BoundingBox, width: Int, height: Int) =
            BoundingBox(
                b.x1.coerceIn(0f, width.toFloat()),
                b.y1.coerceIn(0f, height.toFloat()),
                b.x2.coerceIn(0f, width.toFloat()),
                b.y2.coerceIn(0f, height.toFloat()),
            )

        private fun toTensor(image: RgbImage, size: Int): FloatBuffer {
            val mean = 127.5f
            val std = 128f
            val plane = size * size
            val data = FloatArray(3 * plane) { -mean / std }
            for (y in 0 until image.height) {
                for (x in 0 until image.width) {
                    val s = (y * image.width + x) * 3
                    val d = y * size + x
                    for (c in 0..2) data[c * plane + d] = ((image.pixels[s + c].toInt() and 0xff) - mean) / std
                }
            }
            return FloatBuffer.wrap(data)
        }
    }
}
