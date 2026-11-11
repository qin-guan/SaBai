package sabai.api.unit

import org.assertj.core.api.Assertions.assertThat
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import sabai.api.facerecognition.ArcFaceEmbedder
import sabai.api.facerecognition.FaceAnalyzer
import sabai.api.facerecognition.FaceImages
import sabai.api.facerecognition.FaceModelProvisioner
import sabai.api.facerecognition.FaceRecognitionOptions
import sabai.api.facerecognition.RgbImage
import java.nio.file.Files
import java.nio.file.Path

/**
 * Runs the real SCRFD + ArcFace models. Opt-in, because the models are ~190 MB:
 * SABAI_TEST_MODELS_DIR=<dir with det_10g.onnx, w600k_r50.onnx> and
 * SABAI_TEST_FACES=<photo of person A>,<another photo of person A>.
 */
@EnabledIfEnvironmentVariable(named = "SABAI_TEST_MODELS_DIR", matches = ".+")
class FaceModelTest {
    private val options =
        FaceRecognitionOptions(modelsDirectory = Path.of(System.getenv("SABAI_TEST_MODELS_DIR")), autoDownloadModels = false)
    private val analyzer = FaceAnalyzer(FaceModelProvisioner(options), options)

    @Test
    fun `two photos of the same person match above the threshold`() {
        val (first, second) = System.getenv("SABAI_TEST_FACES").split(",").map { RgbImage.decode(Files.readAllBytes(Path.of(it))) }

        val a = FaceImages.largest(analyzer.analyze(first))!!
        val b = FaceImages.largest(analyzer.analyze(second))!!
        val similarity = ArcFaceEmbedder.similarity(a.embedding, b.embedding)
        println("same-person similarity = $similarity, embedding size = ${a.embedding.size}")

        assertThat(a.embedding).hasSize(512)
        assertThat(a.detection.landmarks).hasSize(5)
        assertThat(a.detection.score).isGreaterThan(0.5f)
        assertThat(similarity).isGreaterThan(options.matchThreshold)
    }

    @Test
    fun `a blank image has no faces`() {
        assertThat(analyzer.analyze(RgbImage(640, 480))).isEmpty()
    }
}
