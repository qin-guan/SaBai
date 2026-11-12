//! Pixel operations: sampling, resizing, shadow lifting and rotation.
//!
//! Rounding is to the nearest value with ties to even, in single precision where the
//! pipeline has always used it, so results stay identical to the embeddings already enrolled.

use super::align::SimilarityTransform;
use super::image::RgbImage;

fn to_byte(value: f32) -> u8 {
    value.round_ties_even().clamp(0.0, 255.0) as u8
}

fn to_byte_f64(value: f64) -> u8 {
    value.round_ties_even().clamp(0.0, 255.0) as u8
}

/// Bilinear sample at (x, y) in pixel coordinates; out-of-bounds neighbours read as black.
pub fn sample_bilinear(image: &RgbImage, x: f32, y: f32) -> [u8; 3] {
    let x0 = x.floor() as i32;
    let y0 = y.floor() as i32;
    let fx = x - x0 as f32;
    let fy = y - y0 as f32;

    let mut acc = [0f32; 3];
    accumulate(image, x0, y0, (1.0 - fx) * (1.0 - fy), &mut acc);
    accumulate(image, x0 + 1, y0, fx * (1.0 - fy), &mut acc);
    accumulate(image, x0, y0 + 1, (1.0 - fx) * fy, &mut acc);
    accumulate(image, x0 + 1, y0 + 1, fx * fy, &mut acc);

    [to_byte(acc[0]), to_byte(acc[1]), to_byte(acc[2])]
}

fn accumulate(image: &RgbImage, x: i32, y: i32, weight: f32, acc: &mut [f32; 3]) {
    if weight == 0.0 || x < 0 || y < 0 || x as usize >= image.width || y as usize >= image.height {
        return;
    }
    let i = (y as usize * image.width + x as usize) * 3;
    acc[0] += image.pixels[i] as f32 * weight;
    acc[1] += image.pixels[i + 1] as f32 * weight;
    acc[2] += image.pixels[i + 2] as f32 * weight;
}

/// Resizes an image. Uses area averaging when shrinking (avoids aliasing on large photos)
/// and bilinear interpolation when enlarging.
pub fn resize(image: &RgbImage, width: usize, height: usize) -> RgbImage {
    if width == image.width && height == image.height {
        return image.clone();
    }

    let scale_x = image.width as f32 / width as f32;
    let scale_y = image.height as f32 / height as f32;
    if !(scale_x >= 1.0 && scale_y >= 1.0) {
        return resize_bilinear(image, width, height);
    }

    let mut output = RgbImage::new(width, height);
    for y in 0..height {
        let sy0 = (y as f32 * scale_y) as usize;
        let sy1 = (sy0 + 1).max(image.height.min(((y + 1) as f32 * scale_y).ceil() as usize));
        for x in 0..width {
            let sx0 = (x as f32 * scale_x) as usize;
            let sx1 = (sx0 + 1).max(image.width.min(((x + 1) as f32 * scale_x).ceil() as usize));
            let (mut r, mut g, mut b, mut total_weight) = (0f64, 0f64, 0f64, 0f64);
            for sy in sy0..sy1 {
                let wy = ((sy + 1) as f32).min((y + 1) as f32 * scale_y) - (sy as f32).max(y as f32 * scale_y);
                let mut i = (sy * image.width + sx0) * 3;
                for sx in sx0..sx1 {
                    let wx = ((sx + 1) as f32).min((x + 1) as f32 * scale_x) - (sx as f32).max(x as f32 * scale_x);
                    let weight = wx.max(0.0) * wy.max(0.0);
                    r += (image.pixels[i] as f32 * weight) as f64;
                    g += (image.pixels[i + 1] as f32 * weight) as f64;
                    b += (image.pixels[i + 2] as f32 * weight) as f64;
                    total_weight += weight as f64;
                    i += 3;
                }
            }

            let o = (y * width + x) * 3;
            output.pixels[o] = to_byte_f64(r / total_weight);
            output.pixels[o + 1] = to_byte_f64(g / total_weight);
            output.pixels[o + 2] = to_byte_f64(b / total_weight);
        }
    }
    output
}

/// Half-pixel bilinear resize, including when shrinking, as used by the SCRFD reference pipeline.
pub fn resize_bilinear(image: &RgbImage, width: usize, height: usize) -> RgbImage {
    if width == image.width && height == image.height {
        return image.clone();
    }
    let mut output = RgbImage::new(width, height);
    let scale_x = image.width as f32 / width as f32;
    let scale_y = image.height as f32 / height as f32;
    let max_x = (image.width - 1) as f32;
    let max_y = (image.height - 1) as f32;
    for y in 0..height {
        let sy = ((y as f32 + 0.5) * scale_y - 0.5).clamp(0.0, max_y);
        for x in 0..width {
            let sx = ((x as f32 + 0.5) * scale_x - 0.5).clamp(0.0, max_x);
            let o = (y * width + x) * 3;
            output.pixels[o..o + 3].copy_from_slice(&sample_bilinear(image, sx, sy));
        }
    }
    output
}

/// Lift shadows for detection only, retaining RGB ratios and limiting noise amplification.
pub fn brighten_shadows(image: &RgbImage) -> RgbImage {
    let mut output = RgbImage::new(image.width, image.height);
    for (source, target) in image.pixels.chunks_exact(3).zip(output.pixels.chunks_exact_mut(3)) {
        let luminance =
            (0.2126 * source[0] as f64 + 0.7152 * source[1] as f64 + 0.0722 * source[2] as f64) / 255.0;
        let gain = if luminance > 0.0 { luminance.powf(-0.35).min(3.0) } else { 1.0 };
        for c in 0..3 {
            target[c] = to_byte_f64(source[c] as f64 * gain);
        }
    }
    output
}

/// Rotate on an expanded canvas. The returned transform maps original to rotated coordinates.
pub fn rotate(image: &RgbImage, degrees: f64) -> (RgbImage, SimilarityTransform) {
    let angle = degrees * std::f64::consts::PI / 180.0;
    let a = angle.cos();
    let b = angle.sin();
    let last_x = image.width as f64 - 1.0;
    let last_y = image.height as f64 - 1.0;
    let width = (a.abs() * last_x + b.abs() * last_y).ceil() as usize + 1;
    let height = (b.abs() * last_x + a.abs() * last_y).ceil() as usize + 1;
    let cx = last_x / 2.0;
    let cy = last_y / 2.0;
    let transform = SimilarityTransform {
        a,
        b,
        tx: (width as f64 - 1.0) / 2.0 - a * cx + b * cy,
        ty: (height as f64 - 1.0) / 2.0 - b * cx - a * cy,
    };
    let inverse = transform.inverse();
    let mut output = RgbImage::new(width, height);
    for y in 0..height {
        for x in 0..width {
            let (sx, sy) = inverse.apply(x as f64, y as f64);
            let o = (y * width + x) * 3;
            output.pixels[o..o + 3].copy_from_slice(&sample_bilinear(image, sx, sy));
        }
    }
    (output, transform)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn resize_bilinear_shrink_uses_half_pixel_coordinates() {
        let image = RgbImage::from_pixels(4, 1, vec![0, 0, 0, 100, 100, 100, 200, 200, 200, 240, 240, 240]);

        let resized = resize_bilinear(&image, 2, 1);

        assert_eq!(resized.pixels, [50, 50, 50, 220, 220, 220]);
    }

    #[test]
    fn brighten_shadows_preserves_black_and_white_and_bounds_gain() {
        let image = RgbImage::from_pixels(3, 1, vec![0, 0, 0, 255, 255, 255, 10, 10, 10]);

        let brightened = brighten_shadows(&image);

        assert_eq!(brightened.pixels[..6], image.pixels[..6]);
        assert!(brightened.pixels[6] > 10 && brightened.pixels[6] <= 30);
        assert_eq!(image.pixels[6], 10);
    }

    #[test]
    fn resize_fractional_shrink_weights_partial_pixels() {
        let image = RgbImage::from_pixels(
            3,
            2,
            vec![0, 0, 0, 120, 120, 120, 240, 240, 240, 0, 0, 0, 120, 120, 120, 240, 240, 240],
        );

        let resized = resize(&image, 2, 1);

        // Each output covers 1.5 columns: a whole edge pixel and half the middle pixel.
        assert_eq!(resized.pixels, [40, 40, 40, 200, 200, 200]);
    }

    #[test]
    fn sample_bilinear_interpolates_channels_independently() {
        let image = RgbImage::from_pixels(2, 1, vec![20, 40, 60, 100, 120, 140]);

        let sample = sample_bilinear(&image, 0.5, 0.0);

        assert_eq!(sample, [60, 80, 100]);
    }

    #[test]
    fn sample_bilinear_outside_image_is_black() {
        let image = RgbImage::from_pixels(1, 1, vec![255, 255, 255]);

        let sample = sample_bilinear(&image, -2.0, -2.0);

        assert_eq!(sample, [0, 0, 0]);
    }
}
