package sabai.api.facerecognition

import io.qdrant.client.QdrantClient
import io.qdrant.client.QdrantGrpcClient
import org.slf4j.LoggerFactory
import org.springframework.boot.context.event.ApplicationReadyEvent
import org.springframework.boot.context.properties.ConfigurationProperties
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.context.event.EventListener

@ConfigurationProperties("sabai.qdrant")
data class QdrantSettings(val host: String = "localhost", val port: Int = 6334, val useTls: Boolean = false, val apiKey: String? = null)

@Configuration
class FaceRecognitionConfig {
    /** The gRPC channel connects lazily, so the API starts even when Qdrant is down. */
    @Bean(destroyMethod = "close")
    fun qdrantClient(settings: QdrantSettings): QdrantClient =
        QdrantClient(
            QdrantGrpcClient.newBuilder(settings.host, settings.port, settings.useTls, false)
                .apply { settings.apiKey?.takeIf { it.isNotBlank() }?.let(::withApiKey) }
                .build(),
        )

    /** FaceRecognitionInitializer: loads the models and prepares the collection in the background. */
    @EventListener(ApplicationReadyEvent::class)
    fun warmUp(event: ApplicationReadyEvent) {
        if (!event.applicationContext.getBean(FaceRecognitionOptions::class.java).warmUpOnStart) return
        val store = event.applicationContext.getBean(FaceStore::class.java)
        Thread.ofVirtual().name("face-warmup").start {
            // Not fatal: requests retry initialisation and surface the error.
            runCatching { store.initialize() }
                .onFailure { LoggerFactory.getLogger(javaClass).error("Face recognition initialisation failed", it) }
        }
    }
}

/** Decodes an upload, turning failures into a 400 on the given field (FaceImages.LoadAsync). */
object FaceImages {
    private const val MAX_PIXELS = 50_000_000L

    fun load(file: org.springframework.web.multipart.MultipartFile?, field: String): RgbImage {
        if (file == null || file.isEmpty) throw sabai.api.domain.FieldValidationException(field, "An image file is required.")
        val image =
            try {
                RgbImage.decode(file.bytes)
            } catch (ex: InvalidImageException) {
                throw sabai.api.domain.FieldValidationException(field, ex.message ?: "Invalid image.")
            }
        if (image.width.toLong() * image.height > MAX_PIXELS) {
            throw sabai.api.domain.FieldValidationException(field, "Image resolution is too large.")
        }
        return image
    }

    fun largest(faces: List<AnalyzedFace>): AnalyzedFace? = faces.maxByOrNull { it.detection.box.area }
}
