package sabai.api.facerecognition

import ai.onnxruntime.OnnxTensor
import ai.onnxruntime.OrtEnvironment
import ai.onnxruntime.OrtSession
import ai.onnxruntime.TensorInfo
import java.nio.FloatBuffer
import kotlin.math.sqrt

/**
 * ArcFace recognition model (InsightFace). Maps an aligned 112x112 face crop to an
 * L2-normalised embedding whose dot product is the cosine similarity.
 */
class ArcFaceEmbedder(private val env: OrtEnvironment, private val session: OrtSession) : AutoCloseable {
    private val inputName = session.inputNames.single()
    private val supportsBatching: Boolean
    val dimensions: Int

    init {
        val input = (session.inputInfo.getValue(inputName).info as TensorInfo).shape
        val output = (session.outputInfo.values.first().info as TensorInfo).shape
        // Some exports declare a dynamic input batch but a fixed output batch of 1.
        supportsBatching = input[0] < 0 && output[0] < 0
        dimensions = output.last().toInt()
        if (dimensions <= 0) throw UnsupportedOperationException("Recognition model has a dynamic embedding size.")
    }

    fun embed(alignedFaces: List<RgbImage>): List<FloatArray> =
        when {
            alignedFaces.isEmpty() -> emptyList()
            supportsBatching -> run(alignedFaces)
            else -> alignedFaces.flatMap { run(listOf(it)) }
        }

    private fun run(faces: List<RgbImage>): List<FloatArray> {
        val size = FaceAligner.SIZE
        val plane = size * size
        val data = FloatArray(faces.size * 3 * plane)
        for ((n, face) in faces.withIndex()) {
            val offset = n * 3 * plane
            for (p in 0 until plane) {
                for (c in 0..2) data[offset + c * plane + p] = ((face.pixels[p * 3 + c].toInt() and 0xff) - MEAN) / STD
            }
        }
        val shape = longArrayOf(faces.size.toLong(), 3, size.toLong(), size.toLong())
        return OnnxTensor.createTensor(env, FloatBuffer.wrap(data), shape).use { tensor ->
            session.run(mapOf(inputName to tensor)).use { results ->
                val output = (results.get(0) as OnnxTensor).floatBuffer
                List(faces.size) { n -> normalize(FloatArray(dimensions) { i -> output[n * dimensions + i] }) }
            }
        }
    }

    override fun close() = session.close()

    companion object {
        private const val MEAN = 127.5f
        private const val STD = 127.5f

        fun normalize(vector: FloatArray): FloatArray {
            val norm = sqrt(vector.sumOf { (it * it).toDouble() }).toFloat()
            if (norm > 0) for (i in vector.indices) vector[i] /= norm
            return vector
        }

        /** Cosine similarity of two L2-normalised embeddings. */
        fun similarity(a: FloatArray, b: FloatArray): Float = a.indices.sumOf { (a[it] * b[it]).toDouble() }.toFloat()
    }
}
