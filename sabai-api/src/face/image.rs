//! Decoded images.

use std::io::Cursor;

use image::{DynamicImage, ImageDecoder, ImageReader};

/// Tightly packed 8-bit RGB image.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RgbImage {
    pub width: usize,
    pub height: usize,
    /// `width * height * 3` bytes, row by row.
    pub pixels: Vec<u8>,
}

/// The bytes are not an image this service can read.
#[derive(Debug, Clone, PartialEq, Eq, thiserror::Error)]
#[error("{0}")]
pub struct InvalidImage(pub String);

/// Where the first stored row and column of an encoded image belong once it is upright
/// (the EXIF orientation tag, values 1 to 8).
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum EncodedOrigin {
    TopLeft = 1,
    TopRight = 2,
    BottomRight = 3,
    BottomLeft = 4,
    LeftTop = 5,
    RightTop = 6,
    RightBottom = 7,
    LeftBottom = 8,
}

impl EncodedOrigin {
    /// From the EXIF orientation value; anything else is upright.
    pub fn from_exif(value: u8) -> Self {
        match value {
            2 => EncodedOrigin::TopRight,
            3 => EncodedOrigin::BottomRight,
            4 => EncodedOrigin::BottomLeft,
            5 => EncodedOrigin::LeftTop,
            6 => EncodedOrigin::RightTop,
            7 => EncodedOrigin::RightBottom,
            8 => EncodedOrigin::LeftBottom,
            _ => EncodedOrigin::TopLeft,
        }
    }
}

impl RgbImage {
    pub fn new(width: usize, height: usize) -> Self {
        RgbImage { width, height, pixels: vec![0; width * height * 3] }
    }

    pub fn from_pixels(width: usize, height: usize, pixels: Vec<u8>) -> Self {
        assert_eq!(pixels.len(), width * height * 3, "Pixel buffer does not match dimensions.");
        RgbImage { width, height, pixels }
    }

    /// Decodes an encoded image (JPEG, PNG, WebP, …), applying its EXIF orientation.
    pub fn decode(bytes: &[u8]) -> Result<RgbImage, InvalidImage> {
        let reader = ImageReader::new(Cursor::new(bytes))
            .with_guessed_format()
            .map_err(|_| InvalidImage("Image could not be read.".to_owned()))?;
        let mut decoder =
            reader.into_decoder().map_err(|_| InvalidImage("Unsupported or corrupt image.".to_owned()))?;
        let origin = decoder
            .orientation()
            .map(|orientation| EncodedOrigin::from_exif(orientation.to_exif()))
            .unwrap_or(EncodedOrigin::TopLeft);
        let decoded = DynamicImage::from_decoder(decoder)
            .map_err(|error| InvalidImage(format!("Image could not be decoded ({error}).")))?;
        // Transparency is dropped, not blended: the colour channels are kept as stored.
        let rgb = decoded.into_rgb8();
        let image = RgbImage { width: rgb.width() as usize, height: rgb.height() as usize, pixels: rgb.into_raw() };
        Ok(Self::orient(image, origin))
    }

    /// Remaps pixels so the image is upright according to its EXIF origin.
    pub fn orient(image: RgbImage, origin: EncodedOrigin) -> RgbImage {
        let (w, h) = (image.width, image.height);
        let map: fn(usize, usize, usize, usize) -> (usize, usize) = match origin {
            EncodedOrigin::TopLeft => return image,
            EncodedOrigin::TopRight => |x, y, w, _| (w - 1 - x, y),
            EncodedOrigin::BottomRight => |x, y, w, h| (w - 1 - x, h - 1 - y),
            EncodedOrigin::BottomLeft => |x, y, _, h| (x, h - 1 - y),
            EncodedOrigin::LeftTop => |x, y, _, _| (y, x),
            EncodedOrigin::RightTop => |x, y, _, h| (y, h - 1 - x),
            EncodedOrigin::RightBottom => |x, y, w, h| (w - 1 - y, h - 1 - x),
            EncodedOrigin::LeftBottom => |x, y, w, _| (w - 1 - y, x),
        };

        let swap = origin >= EncodedOrigin::LeftTop;
        let mut result = if swap { RgbImage::new(h, w) } else { RgbImage::new(w, h) };
        for y in 0..result.height {
            for x in 0..result.width {
                let (sx, sy) = map(x, y, w, h);
                let source = (sy * w + sx) * 3;
                let target = (y * result.width + x) * 3;
                result.pixels[target..target + 3].copy_from_slice(&image.pixels[source..source + 3]);
            }
        }
        result
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// 2x3 image whose red channel encodes the pixel position as 10 * y + x.
    fn sample() -> RgbImage {
        let mut image = RgbImage::new(2, 3);
        for y in 0..3 {
            for x in 0..2 {
                image.pixels[(y * 2 + x) * 3] = (10 * y + x) as u8;
            }
        }
        image
    }

    fn at(image: &RgbImage, x: usize, y: usize) -> u8 {
        image.pixels[(y * image.width + x) * 3]
    }

    #[test]
    fn orient_right_top_rotates_clockwise() {
        let rotated = RgbImage::orient(sample(), EncodedOrigin::RightTop);

        assert_eq!(rotated.width, 3);
        assert_eq!(rotated.height, 2);
        // The bottom-left source pixel ends up top-left.
        assert_eq!(at(&rotated, 0, 0), 20);
        assert_eq!(at(&rotated, 2, 0), 0);
        assert_eq!(at(&rotated, 2, 1), 1);
    }

    #[test]
    fn orient_left_bottom_rotates_counter_clockwise() {
        let rotated = RgbImage::orient(sample(), EncodedOrigin::LeftBottom);

        // The top-right source pixel ends up top-left.
        assert_eq!(at(&rotated, 0, 0), 1);
        assert_eq!(at(&rotated, 0, 1), 0);
        assert_eq!(at(&rotated, 2, 1), 20);
    }

    #[test]
    fn decode_png_round_trip() {
        let bitmap = image::RgbaImage::from_pixel(4, 2, image::Rgba([10, 20, 30, 255]));
        let mut png = Vec::new();
        bitmap.write_to(&mut Cursor::new(&mut png), image::ImageFormat::Png).unwrap();

        let image = RgbImage::decode(&png).unwrap();

        assert_eq!(image.width, 4);
        assert_eq!(image.pixels[..3], [10, 20, 30]);
    }

    #[test]
    fn decode_rejects_garbage() {
        assert!(RgbImage::decode(b"not an image").is_err());
    }
}
