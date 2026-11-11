package sabai.api.facerecognition

import org.slf4j.LoggerFactory
import org.springframework.stereotype.Component
import java.io.FileNotFoundException
import java.net.ProxySelector
import java.net.URI
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.nio.file.Files
import java.nio.file.StandardCopyOption
import java.util.UUID
import java.util.zip.ZipFile
import kotlin.io.path.deleteIfExists
import kotlin.io.path.exists

/** Makes sure the ONNX model files exist locally, downloading the model pack if allowed. */
@Component
class FaceModelProvisioner(private val options: FaceRecognitionOptions) {
    private val log = LoggerFactory.getLogger(javaClass)

    fun ensureModels() {
        val missing = listOf(options.detectorPath, options.recognizerPath).filterNot { it.exists() }
        if (missing.isEmpty()) return
        if (!options.autoDownloadModels) {
            throw FileNotFoundException(
                "Face recognition model(s) not found: ${missing.joinToString()}. Place them in " +
                    "'${options.modelsDirectory}' or enable sabai.face-recognition.auto-download-models.",
            )
        }

        Files.createDirectories(options.modelsDirectory)
        val archive = options.modelsDirectory.resolve("download-${UUID.randomUUID()}.zip")
        try {
            log.info("Downloading face recognition models from {}", options.modelPackUrl)
            val http = HttpClient.newBuilder().followRedirects(HttpClient.Redirect.NORMAL).proxy(ProxySelector.getDefault()).build()
            val response = http.send(HttpRequest.newBuilder(URI.create(options.modelPackUrl)).build(), HttpResponse.BodyHandlers.ofFile(archive))
            check(response.statusCode() in 200..299) { "Model download failed with HTTP ${response.statusCode()}." }

            ZipFile(archive.toFile()).use { zip ->
                for (path in missing) {
                    val name = path.fileName.toString()
                    val entry =
                        zip.entries().asSequence().firstOrNull { it.name.substringAfterLast('/').equals(name, ignoreCase = true) }
                            ?: throw FileNotFoundException("Model '$name' not found in ${options.modelPackUrl}.")
                    val temp = path.resolveSibling("$name.tmp")
                    zip.getInputStream(entry).use { Files.copy(it, temp, StandardCopyOption.REPLACE_EXISTING) }
                    Files.move(temp, path, StandardCopyOption.REPLACE_EXISTING)
                    log.info("Extracted face model {}", path)
                }
            }
        } finally {
            archive.deleteIfExists()
        }
    }
}
