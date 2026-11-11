package sabai.api.storage

import org.slf4j.LoggerFactory
import org.springframework.boot.context.event.ApplicationReadyEvent
import org.springframework.boot.context.properties.ConfigurationProperties
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.context.annotation.Lazy
import org.springframework.context.event.EventListener
import org.springframework.stereotype.Component
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider
import software.amazon.awssdk.core.checksums.RequestChecksumCalculation
import software.amazon.awssdk.core.checksums.ResponseChecksumValidation
import software.amazon.awssdk.regions.Region
import software.amazon.awssdk.services.s3.S3Client
import software.amazon.awssdk.services.s3.model.BucketAlreadyExistsException
import software.amazon.awssdk.services.s3.model.BucketAlreadyOwnedByYouException
import java.net.URI

/** S3-compatible settings for the RustFS bucket (`sabai.storage.*`). */
@ConfigurationProperties("sabai.storage")
data class StorageSettings(
    val bucket: String = "sabai",
    val region: String = "us-east-1",
    val endpoint: String = "",
    val accessKey: String = "",
    val secretKey: String = "",
)

@Configuration
class StorageConfig {
    /** Lazy: only built when an endpoint touches files, so the API starts without RustFS. */
    @Bean
    @Lazy
    fun s3(settings: StorageSettings): S3Client {
        check(settings.endpoint.isNotBlank() && settings.accessKey.isNotBlank() && settings.secretKey.isNotBlank()) {
            "sabai.storage is missing endpoint, access-key or secret-key."
        }
        return S3Client.builder()
            .endpointOverride(URI.create(settings.endpoint))
            .forcePathStyle(true)
            .region(Region.of(settings.region.ifBlank { "us-east-1" }))
            .credentialsProvider(StaticCredentialsProvider.create(AwsBasicCredentials.create(settings.accessKey, settings.secretKey)))
            .requestChecksumCalculation(RequestChecksumCalculation.WHEN_REQUIRED)
            .responseChecksumValidation(ResponseChecksumValidation.WHEN_REQUIRED)
            .build()
    }
}

/** `RustFsS3.EnsureBucketAsync` at startup, retried while RustFS boots. */
@Component
class BucketInitializer(private val settings: StorageSettings, @Lazy private val s3: S3Client) {
    private val log = LoggerFactory.getLogger(javaClass)

    @EventListener(ApplicationReadyEvent::class)
    fun ensureBucket() {
        if (settings.endpoint.isBlank()) return
        for (attempt in 1..10) {
            try {
                s3.createBucket { it.bucket(settings.bucket) }
                return
            } catch (_: BucketAlreadyOwnedByYouException) {
                return
            } catch (_: BucketAlreadyExistsException) {
                return
            } catch (ex: Exception) {
                if (attempt == 10) throw ex
                log.debug("Bucket not ready (attempt {}), retrying", attempt)
                Thread.sleep(1000)
            }
        }
    }
}
