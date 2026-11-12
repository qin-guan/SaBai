//! Aligns a face to the canonical 112x112 ArcFace crop using a least-squares similarity
//! transform (rotation, uniform scale and translation) of its five landmarks.

use super::image::RgbImage;
use super::{PointF, ops};

/// Side of the aligned crop, in pixels.
pub const SIZE: usize = 112;

/// Reference landmark positions used to train InsightFace ArcFace models.
pub const ARC_FACE_TEMPLATE: [PointF; 5] = [
    PointF { x: 38.2946, y: 51.6963 },
    PointF { x: 73.5318, y: 51.5014 },
    PointF { x: 56.0252, y: 71.7366 },
    PointF { x: 41.5493, y: 92.3655 },
    PointF { x: 70.7299, y: 92.2041 },
];

/// The aligned crop of the face with these landmarks, or `None` when they do not
/// determine a transform.
pub fn align(image: &RgbImage, landmarks: &[PointF]) -> Option<RgbImage> {
    let inverse = SimilarityTransform::estimate(landmarks, &ARC_FACE_TEMPLATE)?.inverse();
    let mut output = RgbImage::new(SIZE, SIZE);
    for y in 0..SIZE {
        for x in 0..SIZE {
            let (sx, sy) = inverse.apply(x as f64, y as f64);
            let o = (y * SIZE + x) * 3;
            output.pixels[o..o + 3].copy_from_slice(&ops::sample_bilinear(image, sx, sy));
        }
    }
    Some(output)
}

/// 2D similarity transform: `[x', y'] = [[a, -b], [b, a]] * [x, y] + [tx, ty]`.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct SimilarityTransform {
    pub a: f64,
    pub b: f64,
    pub tx: f64,
    pub ty: f64,
}

impl SimilarityTransform {
    pub fn scale(&self) -> f64 {
        (self.a * self.a + self.b * self.b).sqrt()
    }

    pub fn rotation_radians(&self) -> f64 {
        self.b.atan2(self.a)
    }

    pub fn apply(&self, x: f64, y: f64) -> (f32, f32) {
        ((self.a * x - self.b * y + self.tx) as f32, (self.b * x + self.a * y + self.ty) as f32)
    }

    pub fn inverse(&self) -> SimilarityTransform {
        let d = self.a * self.a + self.b * self.b;
        let ia = self.a / d;
        let ib = -self.b / d;
        SimilarityTransform { a: ia, b: ib, tx: -(ia * self.tx - ib * self.ty), ty: -(ib * self.tx + ia * self.ty) }
    }

    /// Closed-form least-squares fit (Umeyama without reflection) mapping `src` onto `dst`.
    /// `None` without at least two matching point pairs, or when the source points coincide.
    pub fn estimate(src: &[PointF], dst: &[PointF]) -> Option<SimilarityTransform> {
        if src.len() != dst.len() || src.len() < 2 {
            return None;
        }

        let count = src.len() as f64;
        let (mut msx, mut msy, mut mdx, mut mdy) = (0f64, 0f64, 0f64, 0f64);
        for (s, d) in src.iter().zip(dst) {
            msx += s.x as f64;
            msy += s.y as f64;
            mdx += d.x as f64;
            mdy += d.y as f64;
        }
        msx /= count;
        msy /= count;
        mdx /= count;
        mdy /= count;

        let (mut norm, mut dot, mut cross) = (0f64, 0f64, 0f64);
        for (s, d) in src.iter().zip(dst) {
            let (ax, ay) = (s.x as f64 - msx, s.y as f64 - msy);
            let (bx, by) = (d.x as f64 - mdx, d.y as f64 - mdy);
            norm += ax * ax + ay * ay;
            dot += ax * bx + ay * by;
            cross += ax * by - ay * bx;
        }

        if norm.is_nan() || norm < 1e-12 {
            return None;
        }

        let a = dot / norm;
        let b = cross / norm;
        Some(SimilarityTransform { a, b, tx: mdx - (a * msx - b * msy), ty: mdy - (b * msx + a * msy) })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn estimate_recovers_known_transform() {
        let expected = SimilarityTransform { a: 0.8 * 0.3f64.cos(), b: 0.8 * 0.3f64.sin(), tx: 12.0, ty: -7.0 };
        let src = [(10.0, 20.0), (60.0, 25.0), (35.0, 50.0), (15.0, 80.0), (55.0, 82.0)].map(|(x, y)| PointF { x, y });
        let dst = src.map(|p| {
            let (x, y) = expected.apply(p.x as f64, p.y as f64);
            PointF { x, y }
        });

        let actual = SimilarityTransform::estimate(&src, &dst).unwrap();

        assert!((actual.a - expected.a).abs() <= 1e-5);
        assert!((actual.b - expected.b).abs() <= 1e-5);
        assert!((actual.tx - expected.tx).abs() <= 1e-3);
        assert!((actual.ty - expected.ty).abs() <= 1e-3);
    }

    #[test]
    fn inverse_undoes_transform() {
        let t = SimilarityTransform { a: 1.3, b: -0.4, tx: 5.0, ty: 9.0 };
        let (x, y) = t.apply(17.0, 42.0);
        let (bx, by) = t.inverse().apply(x as f64, y as f64);

        assert!((bx - 17.0).abs() <= 1e-3);
        assert!((by - 42.0).abs() <= 1e-3);
    }

    #[test]
    fn align_with_template_landmarks_is_identity_crop() {
        let mut image = RgbImage::new(SIZE, SIZE);
        // Any fixed pseudo-random pattern will do.
        let mut state = 42u32;
        for pixel in &mut image.pixels {
            state = state.wrapping_mul(1_664_525).wrapping_add(1_013_904_223);
            *pixel = (state >> 24) as u8;
        }

        let aligned = align(&image, &ARC_FACE_TEMPLATE).unwrap();

        assert_eq!(aligned.pixels, image.pixels);
    }
}
