package sabai.api.facerecognition

import org.springframework.boot.context.properties.ConfigurationProperties
import java.nio.file.Path

/** Bound from `sabai.face-recognition.*`; invalid values stop the app at startup (ValidateOnStart). */
@ConfigurationProperties("sabai.face-recognition")
data class FaceRecognitionOptions(
    /** Directory holding the ONNX models. */
    val modelsDirectory: Path = Path.of(System.getProperty("user.home"), ".local", "share", "sabai", "models"),
    val detectorModel: String = "det_10g.onnx",
    val recognizerModel: String = "w600k_r50.onnx",
    /** Download missing models from [modelPackUrl] on first use. */
    val autoDownloadModels: Boolean = true,
    val modelPackUrl: String = "https://github.com/deepinsight/insightface/releases/download/v0.7/buffalo_l.zip",
    val detectionSize: Int = 640,
    val detectionThreshold: Float = 0.5f,
    /** Retry missed or small faces at higher resolution, with shadow lifting and roll correction. */
    val enableDetectionFallback: Boolean = true,
    val detectionFallbackSize: Int = 1024,
    val detectionFallbackThreshold: Float = 0.35f,
    val nmsThreshold: Float = 0.4f,
    val minimumFaceSize: Int = 0,
    val minimumEyeDistance: Float = 0f,
    /** Cosine similarity at or above which two faces are the same person. */
    val matchThreshold: Float = 0.4f,
    val collectionName: String = "faces",
    val useCuda: Boolean = false,
    /** Load the models and prepare Qdrant as soon as the app is up. */
    val warmUpOnStart: Boolean = true,
) {
    init {
        require(detectionSize > 0 && detectionSize % 32 == 0) { "detectionSize must be a positive multiple of 32." }
        require(detectionThreshold in 0f..1f) { "detectionThreshold must be between 0 and 1." }
        require(detectionFallbackSize > 0 && detectionFallbackSize % 32 == 0) {
            "detectionFallbackSize must be a positive multiple of 32."
        }
        require(detectionFallbackThreshold in 0f..detectionThreshold) {
            "detectionFallbackThreshold must be between zero and detectionThreshold."
        }
        require(nmsThreshold in 0f..1f) { "nmsThreshold must be between 0 and 1." }
        require(minimumFaceSize >= 0) { "minimumFaceSize must be non-negative." }
        require(minimumEyeDistance.isFinite() && minimumEyeDistance >= 0) { "minimumEyeDistance must be finite and non-negative." }
        require(matchThreshold in -1f..1f) { "matchThreshold must be between -1 and 1." }
    }

    val detectorPath: Path get() = modelsDirectory.resolve(detectorModel)
    val recognizerPath: Path get() = modelsDirectory.resolve(recognizerModel)
}
