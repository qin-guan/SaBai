//! Face detection + recognition pipeline: SCRFD detection, five-point alignment and ArcFace
//! embedding. Models are loaded lazily (and downloaded if needed) on first use.

use std::path::Path;
use std::sync::Arc;

use anyhow::{Context, anyhow};
use ort::execution_providers::CUDAExecutionProvider;
use ort::session::Session;
use ort::session::builder::GraphOptimizationLevel;
use tokio::sync::OnceCell;

use super::detector::ScrfdFaceDetector;
use super::embedder::ArcFaceEmbedder;
use super::image::RgbImage;
use super::{AnalyzedFace, DetectedFace, align, detection, models, quality};
use crate::config::FaceRecognitionSettings;

pub struct FaceAnalyzer {
    options: FaceRecognitionSettings,
    // Set once loading succeeds; a failed load is retried by the next caller.
    models: OnceCell<Arc<Models>>,
}

struct Models {
    detector: ScrfdFaceDetector,
    embedder: ArcFaceEmbedder,
}

impl FaceAnalyzer {
    pub fn new(options: FaceRecognitionSettings) -> Self {
        FaceAnalyzer { options, models: OnceCell::new() }
    }

    pub fn is_ready(&self) -> bool {
        self.models.initialized()
    }

    /// Embedding dimensionality of the loaded recognition model.
    pub async fn embedding_size(&self) -> anyhow::Result<usize> {
        Ok(self.models().await?.embedder.dimensions())
    }

    /// Every face in the image, most confident first.
    pub async fn detect(&self, image: Arc<RgbImage>) -> anyhow::Result<Vec<DetectedFace>> {
        let models = self.models().await?;
        let options = self.options.clone();
        tokio::task::spawn_blocking(move || detect_faces(&models, &options, &image)).await?
    }

    /// Every face usable for recognition, with its embedding.
    pub async fn analyze(&self, image: Arc<RgbImage>) -> anyhow::Result<Vec<AnalyzedFace>> {
        let models = self.models().await?;
        let options = self.options.clone();
        tokio::task::spawn_blocking(move || {
            let mut faces = detect_faces(&models, &options, &image)?;
            faces.retain(|face| quality::is_suitable(face, &options));
            let aligned = faces
                .iter()
                .map(|face| align::align(&image, &face.landmarks).ok_or_else(|| anyhow!("Source points are degenerate.")))
                .collect::<anyhow::Result<Vec<_>>>()?;
            let embeddings = models.embedder.embed(&aligned)?;
            Ok(faces
                .into_iter()
                .zip(embeddings)
                .map(|(detection, embedding)| AnalyzedFace { detection, embedding })
                .collect())
        })
        .await?
    }

    pub async fn warm_up(&self) -> anyhow::Result<()> {
        self.models().await.map(|_| ())
    }

    async fn models(&self) -> anyhow::Result<Arc<Models>> {
        self.models
            .get_or_try_init(|| async {
                models::ensure_models(&self.options).await?;
                let options = self.options.clone();
                let models = tokio::task::spawn_blocking(move || -> anyhow::Result<Models> {
                    let detector = ScrfdFaceDetector::new(create_session(&options.detector_path(), options.use_cuda)?)?;
                    let embedder = ArcFaceEmbedder::new(create_session(&options.recognizer_path(), options.use_cuda)?)?;
                    Ok(Models { detector, embedder })
                })
                .await??;
                tracing::info!("Face models loaded ({}-d embeddings)", models.embedder.dimensions());
                Ok(Arc::new(models))
            })
            .await
            .cloned()
    }
}

fn detect_faces(
    models: &Models,
    options: &FaceRecognitionSettings,
    image: &RgbImage,
) -> anyhow::Result<Vec<DetectedFace>> {
    detection::detect(image, options, &mut |input, size, threshold, nms| {
        models.detector.detect(input, size, threshold, nms)
    })
}

fn create_session(path: &Path, use_cuda: bool) -> anyhow::Result<Session> {
    let builder = || Session::builder()?.with_optimization_level(GraphOptimizationLevel::Level3);
    let mut session = builder()?;
    if use_cuda {
        // Needs a build of ONNX Runtime with the CUDA provider; this one ships the CPU provider only.
        match builder()?.with_execution_providers([CUDAExecutionProvider::default().build().error_on_failure()]) {
            Ok(with_cuda) => session = with_cuda,
            Err(error) => tracing::warn!(%error, "CUDA execution provider unavailable, falling back to CPU"),
        }
    }
    session.commit_from_file(path).with_context(|| format!("Could not load the face model '{}'.", path.display()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::face::embedder::similarity;

    fn photo(name: &str) -> Arc<RgbImage> {
        let path = format!("{}/tests/fixtures/faces/{name}", env!("CARGO_MANIFEST_DIR"));
        Arc::new(RgbImage::decode(&std::fs::read(path).unwrap()).unwrap())
    }

    /// The real models on real photos. Skipped when the model files are not installed.
    #[tokio::test]
    async fn embeddings_tell_people_apart() {
        let options = FaceRecognitionSettings { auto_download_models: false, ..FaceRecognitionSettings::default() };
        if !options.detector_path().is_file() || !options.recognizer_path().is_file() {
            eprintln!("skipped: the face models are not in {}", options.models_directory.display());
            return;
        }
        let analyzer = FaceAnalyzer::new(options.clone());
        assert!(!analyzer.is_ready());
        assert_eq!(analyzer.embedding_size().await.unwrap(), 512);
        assert!(analyzer.is_ready());

        let mut embeddings = Vec::new();
        for name in ["obama-2012.jpg", "obama-2009.jpg", "biden-2021.jpg"] {
            let faces = analyzer.analyze(photo(name)).await.unwrap();
            assert_eq!(faces.len(), 1, "{name}");
            let embedding = faces[0].embedding.clone();
            assert_eq!(embedding.len(), 512);
            let length = embedding.iter().map(|&v| v as f64 * v as f64).sum::<f64>().sqrt();
            assert!((length - 1.0).abs() < 1e-5, "{name}: length {length}");
            assert_eq!(faces[0].detection.landmarks.len(), 5);
            embeddings.push(embedding);
        }

        let same = similarity(&embeddings[0], &embeddings[1]);
        let different = [similarity(&embeddings[0], &embeddings[2]), similarity(&embeddings[1], &embeddings[2])];
        println!("same person: {same}; different people: {different:?}");
        assert!(same >= options.match_threshold, "same person scored {same}");
        assert!(different.iter().all(|&score| score < options.match_threshold), "different people scored {different:?}");

        let detected = analyzer.detect(photo("obama-2012.jpg")).await.unwrap();
        assert_eq!(detected.len(), 1);
    }
}
