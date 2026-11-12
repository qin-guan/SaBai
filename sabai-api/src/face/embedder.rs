//! ArcFace face recognition model (InsightFace). Maps an aligned 112x112 face crop to an
//! L2-normalised identity embedding whose dot product is the cosine similarity.

use std::sync::Mutex;

use anyhow::{Context, bail};
use ort::session::Session;
use ort::value::Tensor;

use super::align;
use super::detector::tensor_dimensions;
use super::image::RgbImage;

const MEAN: f32 = 127.5;
const STD: f32 = 127.5;

pub struct ArcFaceEmbedder {
    session: Mutex<Session>,
    input_name: String,
    supports_batching: bool,
    dimensions: usize,
}

impl ArcFaceEmbedder {
    pub fn new(session: Session) -> anyhow::Result<Self> {
        let [input] = session.inputs.as_slice() else {
            bail!("The recognition model requires a single input.");
        };
        let input_name = input.name.clone();
        let input_dimensions = tensor_dimensions(&input.input_type).unwrap_or_default();
        let output = session.outputs.first().context("The recognition model has no output.")?;
        let output_dimensions = tensor_dimensions(&output.output_type).unwrap_or_default();
        // Some exports declare a dynamic input batch but a fixed output batch of 1.
        let supports_batching =
            input_dimensions.first().is_some_and(|&n| n < 0) && output_dimensions.first().is_some_and(|&n| n < 0);
        let dimensions = output_dimensions.last().copied().unwrap_or(0);
        if dimensions <= 0 {
            bail!("Recognition model has a dynamic embedding size.");
        }
        Ok(ArcFaceEmbedder {
            session: Mutex::new(session),
            input_name,
            supports_batching,
            dimensions: dimensions as usize,
        })
    }

    /// Embedding dimensionality of the model.
    pub fn dimensions(&self) -> usize {
        self.dimensions
    }

    /// One embedding per aligned face, in order.
    pub fn embed(&self, aligned_faces: &[RgbImage]) -> anyhow::Result<Vec<Vec<f32>>> {
        if aligned_faces.is_empty() {
            return Ok(Vec::new());
        }
        if self.supports_batching {
            return self.run(aligned_faces);
        }
        let mut embeddings = Vec::with_capacity(aligned_faces.len());
        for face in aligned_faces {
            embeddings.extend(self.run(std::slice::from_ref(face))?);
        }
        Ok(embeddings)
    }

    fn run(&self, faces: &[RgbImage]) -> anyhow::Result<Vec<Vec<f32>>> {
        const SIZE: usize = align::SIZE;
        const PLANE: usize = SIZE * SIZE;
        let mut data = vec![0f32; faces.len() * 3 * PLANE];
        for (n, face) in faces.iter().enumerate() {
            if face.pixels.len() != PLANE * 3 {
                bail!("Aligned faces must be {SIZE}x{SIZE}.");
            }
            let offset = n * 3 * PLANE;
            for p in 0..PLANE {
                data[offset + p] = (face.pixels[p * 3] as f32 - MEAN) / STD;
                data[offset + PLANE + p] = (face.pixels[p * 3 + 1] as f32 - MEAN) / STD;
                data[offset + 2 * PLANE + p] = (face.pixels[p * 3 + 2] as f32 - MEAN) / STD;
            }
        }
        let input = Tensor::from_array(([faces.len(), 3, SIZE, SIZE], data))?;

        let mut session = self.session.lock().unwrap_or_else(|poisoned| poisoned.into_inner());
        let results = session.run(ort::inputs![self.input_name.as_str() => input])?;
        let (_, output) = results[0].try_extract_tensor::<f32>()?;
        if output.len() < faces.len() * self.dimensions {
            bail!("The recognition model returned fewer values than expected.");
        }
        Ok(output.chunks_exact(self.dimensions).take(faces.len()).map(|chunk| normalize(chunk.to_vec())).collect())
    }
}

/// Scales the vector to unit length (a zero vector is left as it is).
pub fn normalize(mut vector: Vec<f32>) -> Vec<f32> {
    // The squares are summed in double precision, as they always have been.
    let sum: f64 = vector.iter().map(|&v| (v * v) as f64).sum();
    let norm = (sum as f32).sqrt();
    if norm > 0.0 {
        for value in &mut vector {
            *value /= norm;
        }
    }
    vector
}

/// Cosine similarity of two L2-normalised embeddings.
pub fn similarity(a: &[f32], b: &[f32]) -> f32 {
    let mut dot = 0f32;
    for (x, y) in a.iter().zip(b) {
        dot += x * y;
    }
    dot
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn normalize_gives_unit_length_and_keeps_zero() {
        let unit = normalize(vec![3.0, 4.0]);
        assert_eq!(unit, [0.6, 0.8]);
        assert_eq!(similarity(&unit, &unit), 1.0);
        assert_eq!(normalize(vec![0.0, 0.0]), [0.0, 0.0]);
    }
}
