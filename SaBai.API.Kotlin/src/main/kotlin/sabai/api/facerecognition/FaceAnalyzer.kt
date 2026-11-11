package sabai.api.facerecognition

import ai.onnxruntime.OrtEnvironment
import ai.onnxruntime.OrtSession
import jakarta.annotation.PreDestroy
import org.slf4j.LoggerFactory
import org.springframework.stereotype.Component
import java.nio.file.Path

/**
 * Detection + recognition pipeline: SCRFD detection, five-point alignment and ArcFace
 * embedding. Models load (and download if needed) on first use.
 */
@Component
class FaceAnalyzer(private val provisioner: FaceModelProvisioner, private val options: FaceRecognitionOptions) {
    private val log = LoggerFactory.getLogger(javaClass)
    private val env = OrtEnvironment.getEnvironment()

    private class Models(val detector: ScrfdFaceDetector, val embedder: ArcFaceEmbedder)

    // `lazy` replaces the SemaphoreSlim double-check: thread-safe, runs once, and a
    // failed load is retried on the next call because exceptions are not cached.
    private val loader = lazy {
        provisioner.ensureModels()
        Models(ScrfdFaceDetector(env, createSession(options.detectorPath)), ArcFaceEmbedder(env, createSession(options.recognizerPath)))
            .also { log.info("Face models loaded ({}-d embeddings)", it.embedder.dimensions) }
    }
    private val models by loader

    val embeddingSize: Int get() = models.embedder.dimensions

    fun detect(image: RgbImage): List<DetectedFace> = BestEffortFaceDetection.detect(image, options, models.detector::detect)

    fun analyze(image: RgbImage): List<AnalyzedFace> {
        val faces = detect(image).filter { FaceQuality.isSuitable(it, options) }
        val embeddings = models.embedder.embed(faces.map { FaceAligner.align(image, it.landmarks) })
        return faces.zip(embeddings, ::AnalyzedFace)
    }

    fun warmUp() {
        models
    }

    private fun createSession(path: Path): OrtSession {
        val sessionOptions = OrtSession.SessionOptions().apply { setOptimizationLevel(OrtSession.SessionOptions.OptLevel.ALL_OPT) }
        if (options.useCuda) {
            runCatching { sessionOptions.addCUDA() }
                .onFailure { log.warn("CUDA execution provider unavailable, falling back to CPU", it) }
        }
        return env.createSession(path.toString(), sessionOptions)
    }

    @PreDestroy
    fun close() {
        if (loader.isInitialized()) {
            models.detector.close()
            models.embedder.close()
        }
    }
}
