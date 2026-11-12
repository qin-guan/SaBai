//! Resolution and geometry guards applied before generating identity embeddings.

use super::DetectedFace;
use super::align::{ARC_FACE_TEMPLATE, SimilarityTransform};
use crate::config::FaceRecognitionSettings;

pub fn is_suitable(face: &DetectedFace, options: &FaceRecognitionSettings) -> bool {
    let bbox = face.bbox;
    let minimum_face_size = options.minimum_face_size as f32;
    if !face.score.is_finite()
        || !bbox.x1.is_finite()
        || !bbox.y1.is_finite()
        || !bbox.x2.is_finite()
        || !bbox.y2.is_finite()
        || bbox.width() <= 0.0
        || bbox.height() <= 0.0
        || bbox.width() < minimum_face_size
        || bbox.height() < minimum_face_size
    {
        return false;
    }

    if face.landmarks.len() != 5 || face.landmarks.iter().any(|p| !p.x.is_finite() || !p.y.is_finite()) {
        return false;
    }

    let dx = face.landmarks[1].x as f64 - face.landmarks[0].x as f64;
    let dy = face.landmarks[1].y as f64 - face.landmarks[0].y as f64;
    let eye_distance = (dx * dx + dy * dy).sqrt();
    if eye_distance <= 1e-5 || eye_distance < options.minimum_eye_distance as f64 {
        return false;
    }

    // Distinct eyes alone do not guarantee an invertible alignment transform.
    SimilarityTransform::estimate(&face.landmarks, &ARC_FACE_TEMPLATE)
        .is_some_and(|transform| transform.scale().is_finite() && transform.scale() > 1e-6)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::face::{BoundingBox, PointF};

    fn face() -> DetectedFace {
        DetectedFace { bbox: BoundingBox::new(0.0, 0.0, 112.0, 112.0), score: 0.9, landmarks: ARC_FACE_TEMPLATE.to_vec() }
    }

    fn options() -> FaceRecognitionSettings {
        FaceRecognitionSettings::default()
    }

    #[test]
    fn accepts_well_resolved_face() {
        assert!(is_suitable(&face(), &options()));
    }

    #[test]
    fn rejects_small_face() {
        let face = DetectedFace { bbox: BoundingBox::new(0.0, 0.0, 39.0, 112.0), ..face() };

        assert!(!is_suitable(&face, &FaceRecognitionSettings { minimum_face_size: 40, ..options() }));
    }

    #[test]
    fn rejects_insufficient_eye_resolution() {
        let mut face = face();
        face.landmarks[1] = PointF { x: face.landmarks[0].x + 5.0, ..face.landmarks[0] };

        assert!(!is_suitable(&face, &FaceRecognitionSettings { minimum_eye_distance: 12.0, ..options() }));
    }

    #[test]
    fn rejects_non_finite_landmarks() {
        let mut face = face();
        face.landmarks[2] = PointF { x: f32::NAN, y: 50.0 };

        assert!(!is_suitable(&face, &options()));
    }

    #[test]
    fn rejects_degenerate_landmarks_even_with_size_guards_disabled() {
        let face = DetectedFace { landmarks: vec![PointF { x: 50.0, y: 50.0 }; 5], ..face() };
        let options = FaceRecognitionSettings { minimum_face_size: 0, minimum_eye_distance: 0.0, ..options() };

        assert!(!is_suitable(&face, &options));
    }

    #[test]
    fn size_guards_can_be_disabled() {
        let face = DetectedFace {
            bbox: BoundingBox::new(0.0, 0.0, 20.0, 20.0),
            landmarks: ARC_FACE_TEMPLATE.iter().map(|p| PointF { x: p.x / 10.0, y: p.y / 10.0 }).collect(),
            ..face()
        };
        let options = FaceRecognitionSettings { minimum_face_size: 0, minimum_eye_distance: 0.0, ..options() };

        assert!(is_suitable(&face, &options));
    }

    #[test]
    fn defaults_accept_small_angled_face() {
        let transform = SimilarityTransform { a: 0.15, b: 0.1, tx: 2.0, ty: 1.0 };
        let face = DetectedFace {
            bbox: BoundingBox::new(0.0, 0.0, 24.0, 24.0),
            landmarks: ARC_FACE_TEMPLATE
                .iter()
                .map(|p| {
                    let (x, y) = transform.apply(p.x as f64, p.y as f64);
                    PointF { x, y }
                })
                .collect(),
            ..face()
        };

        assert!(is_suitable(&face, &options()));
    }
}
