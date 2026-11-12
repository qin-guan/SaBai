//! Bounded detection retries; recognition always samples pixels from the original photo.

use super::align::SimilarityTransform;
use super::detector::non_max_suppression;
use super::image::RgbImage;
use super::{BoundingBox, DetectedFace, PointF, ops, quality};
use crate::config::FaceRecognitionSettings;

/// One detector pass: `(image, input size, score threshold, NMS threshold)`.
pub type DetectPass<'a> = dyn FnMut(&RgbImage, u32, f32, f32) -> anyhow::Result<Vec<DetectedFace>> + 'a;

pub fn detect(
    image: &RgbImage,
    options: &FaceRecognitionSettings,
    detect: &mut DetectPass<'_>,
) -> anyhow::Result<Vec<DetectedFace>> {
    let geometry_only = FaceRecognitionSettings { minimum_face_size: 0, minimum_eye_distance: 0.0, ..options.clone() };
    let mut run = |input: &RgbImage, size: u32, threshold: f32| -> anyhow::Result<Vec<DetectedFace>> {
        // Reject invalid geometry before it can suppress usable detections across passes.
        let mut faces = detect(input, size, threshold, options.nms_threshold)?;
        faces.retain(|face| quality::is_suitable(face, &geometry_only));
        Ok(faces)
    };

    let primary = run(image, options.detection_size, options.detection_threshold)?;
    if !options.enable_detection_fallback
        || (!primary.is_empty() && primary.iter().all(|f| f.bbox.width().min(f.bbox.height()) >= 64.0))
    {
        return Ok(primary);
    }

    let size = options.detection_size.max(options.detection_fallback_size);
    let retry = run(image, size, options.detection_fallback_threshold)?;
    // Keep existing primary landmarks, rather than replacing them with a different pass.
    let merged = merge(primary, retry, options.nms_threshold);
    if !merged.is_empty() {
        return Ok(merged);
    }

    let brightened = ops::brighten_shadows(image);
    let lighting = run(&brightened, size, options.detection_fallback_threshold)?;
    if !lighting.is_empty() {
        return Ok(lighting);
    }

    // Roll retries are only used after unrotated passes fail. They do not synthesize
    // hidden facial features or compensate for a full profile/yaw angle.
    let mut rotated_faces = Vec::new();
    for degrees in [-25.0, 25.0] {
        let (rotated, transform) = ops::rotate(&brightened, degrees);
        let inverse = transform.inverse();
        rotated_faces.extend(
            run(&rotated, size, options.detection_fallback_threshold)?
                .into_iter()
                .map(|face| map_to_original(face, &inverse, image.width, image.height))
                .filter(|face| quality::is_suitable(face, &geometry_only)),
        );
    }

    Ok(non_max_suppression(rotated_faces, options.nms_threshold))
}

pub(crate) fn merge(primary: Vec<DetectedFace>, retry: Vec<DetectedFace>, nms_threshold: f32) -> Vec<DetectedFace> {
    let additional: Vec<DetectedFace> =
        retry.into_iter().filter(|f| primary.iter().all(|p| p.bbox.iou(&f.bbox) <= nms_threshold)).collect();
    let mut merged = primary;
    merged.extend(non_max_suppression(additional, nms_threshold));
    sort_by_score_descending(&mut merged);
    merged
}

/// Highest score first; equal scores keep their order.
pub(crate) fn sort_by_score_descending(faces: &mut [DetectedFace]) {
    faces.sort_by(|a, b| b.score.partial_cmp(&a.score).unwrap_or(std::cmp::Ordering::Equal));
}

pub(crate) fn map_to_original(
    face: DetectedFace,
    inverse: &SimilarityTransform,
    width: usize,
    height: usize,
) -> DetectedFace {
    let bbox = face.bbox;
    let corners = [
        inverse.apply(bbox.x1 as f64, bbox.y1 as f64),
        inverse.apply(bbox.x2 as f64, bbox.y1 as f64),
        inverse.apply(bbox.x1 as f64, bbox.y2 as f64),
        inverse.apply(bbox.x2 as f64, bbox.y2 as f64),
    ];
    let min = |pick: fn(&(f32, f32)) -> f32| corners.iter().map(pick).fold(f32::INFINITY, f32::min);
    let max = |pick: fn(&(f32, f32)) -> f32| corners.iter().map(pick).fold(f32::NEG_INFINITY, f32::max);
    let (width, height) = (width as f32, height as f32);
    DetectedFace {
        bbox: BoundingBox::new(
            min(|p| p.0).clamp(0.0, width),
            min(|p| p.1).clamp(0.0, height),
            max(|p| p.0).clamp(0.0, width),
            max(|p| p.1).clamp(0.0, height),
        ),
        score: face.score,
        landmarks: face
            .landmarks
            .iter()
            .map(|p| {
                let (x, y) = inverse.apply(p.x as f64, p.y as f64);
                PointF { x, y }
            })
            .collect(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::face::align::ARC_FACE_TEMPLATE;

    fn face(score: f32) -> DetectedFace {
        DetectedFace { bbox: BoundingBox::new(0.0, 0.0, 112.0, 112.0), score, landmarks: ARC_FACE_TEMPLATE.to_vec() }
    }

    fn options() -> FaceRecognitionSettings {
        FaceRecognitionSettings::default()
    }

    #[test]
    fn good_primary_detection_does_not_retry() {
        let mut calls = 0;
        let mut pass = |_: &RgbImage, _: u32, _: f32, _: f32| {
            calls += 1;
            Ok(vec![face(0.9)])
        };

        let result = detect(&RgbImage::new(112, 112), &options(), &mut pass).unwrap();

        assert_eq!(calls, 1);
        assert_eq!(result.len(), 1);
    }

    #[test]
    fn missed_face_retries_at_higher_resolution_without_changing_match_threshold() {
        let mut calls = 0;
        let mut last_size = 0;
        let mut last_threshold = 0.0;
        let mut pass = |_: &RgbImage, size: u32, threshold: f32, _: f32| {
            calls += 1;
            last_size = size;
            last_threshold = threshold;
            Ok(if calls == 1 { vec![] } else { vec![face(0.4)] })
        };

        let options = options();
        let result = detect(&RgbImage::new(112, 112), &options, &mut pass).unwrap();

        assert_eq!(calls, 2);
        assert_eq!(last_size, 1024);
        assert_eq!(last_threshold, 0.35);
        assert_eq!(options.match_threshold, 0.4);
        assert_eq!(result.len(), 1);
    }

    #[test]
    fn lighting_retry_does_not_modify_original_pixels() {
        let mut source = RgbImage::new(112, 112);
        source.pixels.fill(20);
        let mut calls = 0;
        let mut retry_pixel = 0;
        let mut pass = |image: &RgbImage, _: u32, _: f32, _: f32| {
            calls += 1;
            retry_pixel = image.pixels[0];
            Ok(if calls < 3 { vec![] } else { vec![face(0.9)] })
        };

        let result = detect(&source, &options(), &mut pass).unwrap();

        assert_eq!(calls, 3);
        assert!(retry_pixel > 20);
        assert!(source.pixels.iter().all(|&p| p == 20));
        assert_eq!(result.len(), 1);
    }

    #[test]
    fn fallback_can_be_disabled() {
        let mut calls = 0;
        let mut pass = |_: &RgbImage, _: u32, _: f32, _: f32| {
            calls += 1;
            Ok(vec![])
        };

        let options = FaceRecognitionSettings { enable_detection_fallback: false, ..options() };
        let result = detect(&RgbImage::new(112, 112), &options, &mut pass).unwrap();

        assert_eq!(calls, 1);
        assert_eq!(result.len(), 0);
    }

    #[test]
    fn no_face_uses_at_most_five_passes() {
        let mut calls = 0;
        let mut pass = |_: &RgbImage, _: u32, _: f32, _: f32| {
            calls += 1;
            Ok(vec![])
        };

        let result = detect(&RgbImage::new(112, 112), &options(), &mut pass).unwrap();

        assert_eq!(calls, 5);
        assert_eq!(result.len(), 0);
    }

    #[test]
    fn small_primary_face_is_kept_during_retry() {
        let mut calls = 0;
        let small = DetectedFace { bbox: BoundingBox::new(0.0, 0.0, 24.0, 24.0), ..face(0.6) };
        let mut pass = |_: &RgbImage, _: u32, _: f32, _: f32| {
            calls += 1;
            Ok(if calls == 1 { vec![small.clone()] } else { vec![] })
        };

        let result = detect(&RgbImage::new(112, 112), &options(), &mut pass).unwrap();

        assert_eq!(calls, 2);
        assert_eq!(result[0], small);
    }

    #[test]
    fn merge_preserves_primary_landmarks_when_retry_overlaps() {
        let primary = face(0.6);
        let retry = DetectedFace { bbox: BoundingBox::new(1.0, 1.0, 111.0, 111.0), ..face(0.9) };

        let result = merge(vec![primary.clone()], vec![retry], 0.4);

        assert_eq!(result.len(), 1);
        assert_eq!(result[0], primary);
    }

    #[test]
    fn map_to_original_restores_landmarks_after_rotation() {
        let original = RgbImage::new(112, 112);
        let (rotated, transform) = ops::rotate(&original, 25.0);
        let face = DetectedFace {
            bbox: BoundingBox::new(0.0, 0.0, rotated.width as f32, rotated.height as f32),
            landmarks: ARC_FACE_TEMPLATE
                .iter()
                .map(|p| {
                    let (x, y) = transform.apply(p.x as f64, p.y as f64);
                    PointF { x, y }
                })
                .collect(),
            ..face(0.9)
        };

        let mapped = map_to_original(face, &transform.inverse(), 112, 112);

        for (landmark, expected) in mapped.landmarks.iter().zip(&ARC_FACE_TEMPLATE) {
            assert!((landmark.x - expected.x).abs() <= 1e-3);
            assert!((landmark.y - expected.y).abs() <= 1e-3);
        }
        assert_eq!(mapped.bbox.x1, 0.0);
        assert_eq!(mapped.bbox.x2, 112.0);
    }
}
