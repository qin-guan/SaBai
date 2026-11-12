//! Face recognition: detect faces, turn them into embeddings, and search enrolled people.
//!
//! [`FaceService`] is what endpoints use. Models load on first use (and at startup in
//! the background); embeddings are kept in a [`FaceIndex`].

pub mod image;

use uuid::Uuid;

pub use image::{InvalidImage, RgbImage};

use crate::config::FaceRecognitionSettings;
use crate::time::Ts;

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

#[derive(Debug, Clone, PartialEq)]
pub struct StoredFace {
    pub face_id: Uuid,
    pub person_id: String,
    pub created_at: Ts,
}

#[derive(Debug, Clone, PartialEq)]
pub struct FaceMatch {
    pub face_id: Uuid,
    pub person_id: String,
    pub similarity: f32,
}

/// Placeholder until the pipeline is ported; see the module docs.
pub struct FaceService {
    pub settings: FaceRecognitionSettings,
}

impl FaceService {
    /// Removes every enrolled face of a person. Returns how many were removed.
    pub async fn delete_person(&self, _person_id: &str) -> anyhow::Result<u64> {
        Ok(0)
    }
}
