//! Face recognition: detect faces, turn them into embeddings, and search enrolled people.
//!
//! 1. **Detection** ([`detector`]): SCRFD finds face boxes and five landmarks, with
//!    bounded retries for missed, small, dark or tilted faces ([`detection`]).
//! 2. **Alignment** ([`align`]): the landmarks are mapped onto the ArcFace 112×112 template.
//! 3. **Recognition** ([`embedder`]): ArcFace produces an L2-normalised embedding.
//! 4. **Search** ([`store`]): embeddings live in a [`FaceIndex`], Qdrant in production.
//!
//! [`FaceService`] is what endpoints use. Models load on first use (and at startup in
//! the background, see [`FaceService::start_background_initialization`]).

pub mod align;
pub mod analyzer;
pub mod detection;
pub mod detector;
pub mod embedder;
pub mod image;
pub mod models;
pub mod ops;
pub mod qdrant;
pub mod quality;
pub mod store;

use std::sync::Arc;

use uuid::Uuid;

pub use analyzer::FaceAnalyzer;
pub use image::{EncodedOrigin, InvalidImage, RgbImage};
pub use store::{FaceIndex, FaceMatch, FaceStore, InMemoryFaceIndex, StoredFace};

use crate::config::{FaceRecognitionSettings, QdrantSettings};

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct PointF {
    pub x: f32,
    pub y: f32,
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct BoundingBox {
    pub x1: f32,
    pub y1: f32,
    pub x2: f32,
    pub y2: f32,
}

impl BoundingBox {
    pub fn new(x1: f32, y1: f32, x2: f32, y2: f32) -> Self {
        BoundingBox { x1, y1, x2, y2 }
    }

    pub fn width(&self) -> f32 {
        (self.x2 - self.x1).max(0.0)
    }

    pub fn height(&self) -> f32 {
        (self.y2 - self.y1).max(0.0)
    }

    pub fn area(&self) -> f32 {
        self.width() * self.height()
    }

    /// Intersection over union with another box.
    pub fn iou(&self, other: &BoundingBox) -> f32 {
        let w = self.x2.min(other.x2) - self.x1.max(other.x1);
        let h = self.y2.min(other.y2) - self.y1.max(other.y1);
        if w <= 0.0 || h <= 0.0 {
            return 0.0;
        }
        let inter = w * h;
        inter / (self.area() + other.area() - inter)
    }
}

/// A detected face: bounding box, detector confidence and the five facial landmarks
/// (left eye, right eye, nose tip, left mouth corner, right mouth corner).
#[derive(Debug, Clone, PartialEq)]
pub struct DetectedFace {
    pub bbox: BoundingBox,
    pub score: f32,
    pub landmarks: Vec<PointF>,
}

/// A detected face together with its L2-normalised identity embedding.
#[derive(Debug, Clone, PartialEq)]
pub struct AnalyzedFace {
    pub detection: DetectedFace,
    pub embedding: Vec<f32>,
}

/// The index faces are kept in when Qdrant is configured, otherwise `None`.
pub fn configured_index(
    qdrant: &QdrantSettings,
    settings: &FaceRecognitionSettings,
) -> anyhow::Result<Option<Arc<dyn FaceIndex>>> {
    Ok(qdrant::QdrantFaceIndex::from_settings(qdrant, &settings.collection_name)?
        .map(|index| Arc::new(index) as Arc<dyn FaceIndex>))
}

/// The face pipeline and the store of enrolled faces.
#[derive(Clone)]
pub struct FaceService {
    pub settings: FaceRecognitionSettings,
    analyzer: Arc<FaceAnalyzer>,
    store: Arc<FaceStore>,
}

impl FaceService {
    /// `index` is where enrolled faces are kept. Without one, detection and comparison
    /// still work and everything that needs enrolled faces fails when used.
    pub fn new(settings: FaceRecognitionSettings, index: Option<Arc<dyn FaceIndex>>) -> Self {
        let analyzer = Arc::new(FaceAnalyzer::new(settings.clone()));
        let store = Arc::new(FaceStore::new(index, analyzer.clone()));
        FaceService { settings, analyzer, store }
    }

    /// Loads the models and prepares the collection in the background at startup.
    pub fn start_background_initialization(&self) {
        let store = self.store.clone();
        tokio::spawn(async move {
            // Not fatal: requests retry initialisation and surface the error.
            if let Err(error) = store.initialize().await {
                tracing::error!(error = format!("{error:#}"), "Face recognition initialisation failed");
            }
        });
    }

    /// Whether the models are loaded.
    pub fn is_ready(&self) -> bool {
        self.analyzer.is_ready()
    }

    /// Every face in the image with its box, score and landmarks.
    pub async fn detect(&self, image: Arc<RgbImage>) -> anyhow::Result<Vec<DetectedFace>> {
        self.analyzer.detect(image).await
    }

    /// Every face usable for recognition, with its embedding.
    pub async fn analyze(&self, image: Arc<RgbImage>) -> anyhow::Result<Vec<AnalyzedFace>> {
        self.analyzer.analyze(image).await
    }

    /// Enrolls an embedding for a person.
    pub async fn add(&self, person_id: &str, embedding: &[f32]) -> anyhow::Result<StoredFace> {
        self.store.add(person_id, embedding).await
    }

    /// The best match per person, most similar first.
    pub async fn search(&self, embedding: &[f32], min_similarity: f32, limit: i32) -> anyhow::Result<Vec<FaceMatch>> {
        self.store.search(embedding, min_similarity, limit).await
    }

    /// A person's enrolled faces, oldest first.
    pub async fn list(&self, person_id: &str) -> anyhow::Result<Vec<StoredFace>> {
        self.store.list(person_id).await
    }

    /// Removes one enrolled face; `false` when there is no such face.
    pub async fn delete_face(&self, face_id: Uuid) -> anyhow::Result<bool> {
        self.store.delete_face(face_id).await
    }

    /// Removes every enrolled face of a person. Returns how many were removed.
    pub async fn delete_person(&self, person_id: &str) -> anyhow::Result<u64> {
        self.store.delete_person(person_id).await
    }
}

/// The most prominent face: the one with the largest box.
pub fn largest(faces: &[AnalyzedFace]) -> Option<&AnalyzedFace> {
    let mut best: Option<&AnalyzedFace> = None;
    for face in faces {
        if best.is_none_or(|kept| face.detection.bbox.area() > kept.detection.bbox.area()) {
            best = Some(face);
        }
    }
    best
}
