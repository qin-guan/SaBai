//! SCRFD face detector (InsightFace). Produces boxes and five-point landmarks from the
//! stride 8/16/32 heads of the model.

use std::sync::Mutex;

use anyhow::{Context, bail};
use ort::session::Session;
use ort::value::{Tensor, ValueType};

use super::image::RgbImage;
use super::{BoundingBox, DetectedFace, PointF, ops};

const STRIDES: [usize; 3] = [8, 16, 32];
const ANCHORS_PER_LOCATION: usize = 2;

pub struct ScrfdFaceDetector {
    // Running a session needs exclusive access, so detections queue up here.
    session: Mutex<Session>,
    input_name: String,
    output_names: Vec<String>,
    fixed_input_size: Option<usize>,
}

/// The dimensions a model declares for a tensor input or output; `-1` where dynamic.
pub(crate) fn tensor_dimensions(value_type: &ValueType) -> Option<Vec<i64>> {
    match value_type {
        ValueType::Tensor { shape, .. } => Some(shape.to_vec()),
        _ => None,
    }
}

impl ScrfdFaceDetector {
    pub fn new(session: Session) -> anyhow::Result<Self> {
        let [input] = session.inputs.as_slice() else {
            bail!("SCRFD requires a single input.");
        };
        let input_name = input.name.clone();
        let output_names: Vec<String> = session.outputs.iter().map(|output| output.name.clone()).collect();
        let dimensions = tensor_dimensions(&input.input_type).unwrap_or_default();
        if dimensions.len() != 4 || (dimensions[1] > 0 && dimensions[1] != 3) {
            bail!("SCRFD requires an NCHW RGB input.");
        }
        let mut fixed_input_size = None;
        if dimensions[2] > 0 || dimensions[3] > 0 {
            if dimensions[2] <= 0 || dimensions[2] != dimensions[3] || dimensions[2] % 32 != 0 {
                bail!("SCRFD requires a square input with sides divisible by 32.");
            }
            fixed_input_size = Some(dimensions[2] as usize);
        }

        // Landmark-capable SCRFD models (det_*g, *_kps) with 3 strides expose 9 outputs:
        // scores, box distances and landmark offsets for each stride, with 2 anchors per cell.
        if output_names.len() != 9 {
            bail!(
                "Unsupported SCRFD model with {} outputs; a 3-stride model with landmarks is required.",
                output_names.len()
            );
        }

        Ok(ScrfdFaceDetector { session: Mutex::new(session), input_name, output_names, fixed_input_size })
    }

    pub fn detect(
        &self,
        image: &RgbImage,
        input_size: u32,
        score_threshold: f32,
        nms_threshold: f32,
    ) -> anyhow::Result<Vec<DetectedFace>> {
        let input_size = self.fixed_input_size.unwrap_or(input_size as usize);
        // Letterbox: keep the aspect ratio and pad the bottom/right edge.
        let ratio = image.height as f32 / image.width as f32;
        let (new_width, new_height);
        if ratio > 1.0 {
            new_height = input_size;
            new_width = ((new_height as f32 / ratio) as usize).max(1);
        } else {
            new_width = input_size;
            new_height = ((new_width as f32 * ratio) as usize).max(1);
        }

        // Integer resize dimensions can produce slightly different scales per axis.
        let scale_x = new_width as f32 / image.width as f32;
        let scale_y = new_height as f32 / image.height as f32;
        let resized = ops::resize_bilinear(image, new_width, new_height);
        let input = Tensor::from_array(([1, 3, input_size, input_size], to_tensor(&resized, input_size)))?;

        let mut session = self.session.lock().unwrap_or_else(|poisoned| poisoned.into_inner());
        let results = session.run(ort::inputs![self.input_name.as_str() => input])?;
        let mut outputs = Vec::with_capacity(self.output_names.len());
        for name in &self.output_names {
            let value = results.get(name).with_context(|| format!("SCRFD did not return output '{name}'."))?;
            outputs.push(value.try_extract_tensor::<f32>()?.1);
        }

        let mut candidates = Vec::new();
        for (level, &stride) in STRIDES.iter().enumerate() {
            let scores = outputs[level];
            let boxes = outputs[level + STRIDES.len()];
            let kps = outputs[level + STRIDES.len() * 2];
            let grid_w = input_size / stride;
            let grid_h = input_size / stride;
            let count = grid_w * grid_h * ANCHORS_PER_LOCATION;
            if scores.len() < count || boxes.len() < count * 4 || kps.len() < count * 10 {
                bail!("SCRFD returned fewer values than a {input_size}-pixel input implies.");
            }
            let stride_f = stride as f32;

            for i in 0..count {
                let score = scores[i];
                if !score.is_finite() || score < score_threshold {
                    continue;
                }

                let cell = i / ANCHORS_PER_LOCATION;
                let cx = (cell % grid_w * stride) as f32;
                let cy = (cell / grid_w * stride) as f32;

                let bbox = BoundingBox::new(
                    (cx - boxes[i * 4] * stride_f) / scale_x,
                    (cy - boxes[i * 4 + 1] * stride_f) / scale_y,
                    (cx + boxes[i * 4 + 2] * stride_f) / scale_x,
                    (cy + boxes[i * 4 + 3] * stride_f) / scale_y,
                );

                let landmarks = (0..5)
                    .map(|k| PointF {
                        x: (cx + kps[i * 10 + k * 2] * stride_f) / scale_x,
                        y: (cy + kps[i * 10 + k * 2 + 1] * stride_f) / scale_y,
                    })
                    .collect();

                candidates.push(DetectedFace { bbox, score, landmarks });
            }
        }

        let mut faces = non_max_suppression(candidates, nms_threshold);
        for face in &mut faces {
            face.bbox = clamp(face.bbox, image.width, image.height);
        }
        Ok(faces)
    }
}

/// Keeps the highest-scoring face of each group of overlapping boxes.
pub fn non_max_suppression(mut faces: Vec<DetectedFace>, iou_threshold: f32) -> Vec<DetectedFace> {
    super::detection::sort_by_score_descending(&mut faces);
    let mut kept: Vec<DetectedFace> = Vec::new();
    for face in faces {
        if kept.iter().all(|k| k.bbox.iou(&face.bbox) <= iou_threshold) {
            kept.push(face);
        }
    }
    kept
}

fn clamp(b: BoundingBox, width: usize, height: usize) -> BoundingBox {
    let (width, height) = (width as f32, height as f32);
    BoundingBox::new(b.x1.clamp(0.0, width), b.y1.clamp(0.0, height), b.x2.clamp(0.0, width), b.y2.clamp(0.0, height))
}

/// Planar, normalised pixels in the top-left corner of a `size`-square canvas.
fn to_tensor(image: &RgbImage, size: usize) -> Vec<f32> {
    const MEAN: f32 = 127.5;
    const STD: f32 = 128.0;
    let plane = size * size;
    let mut data = vec![-MEAN / STD; 3 * plane];
    for y in 0..image.height {
        for x in 0..image.width {
            let s = (y * image.width + x) * 3;
            let d = y * size + x;
            data[d] = (image.pixels[s] as f32 - MEAN) / STD;
            data[plane + d] = (image.pixels[s + 1] as f32 - MEAN) / STD;
            data[2 * plane + d] = (image.pixels[s + 2] as f32 - MEAN) / STD;
        }
    }
    data
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn iou_computes_overlap() {
        let a = BoundingBox::new(0.0, 0.0, 10.0, 10.0);
        let b = BoundingBox::new(5.0, 0.0, 15.0, 10.0);

        assert!((a.iou(&b) - 50.0 / 150.0).abs() <= 1e-6);
        assert_eq!(a.iou(&BoundingBox::new(20.0, 20.0, 30.0, 30.0)), 0.0);
    }

    #[test]
    fn non_max_suppression_keeps_highest_scoring_of_overlapping_boxes() {
        let face = |x1, y1, x2, y2, score| DetectedFace { bbox: BoundingBox::new(x1, y1, x2, y2), score, landmarks: vec![] };
        let faces = vec![
            face(0.0, 0.0, 10.0, 10.0, 0.7),
            face(1.0, 1.0, 11.0, 11.0, 0.9),
            face(50.0, 50.0, 60.0, 60.0, 0.6),
        ];

        let kept = non_max_suppression(faces, 0.4);

        assert_eq!(kept.iter().map(|f| f.score).collect::<Vec<_>>(), [0.9, 0.6]);
    }
}
