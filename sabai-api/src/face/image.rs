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
        let orientation = decoder.orientation().unwrap_or(image::metadata::Orientation::NoTransforms);
        let mut decoded = DynamicImage::from_decoder(decoder)
            .map_err(|error| InvalidImage(format!("Image could not be decoded ({error}).")))?;
        decoded.apply_orientation(orientation);
        let rgb = decoded.into_rgb8();
        Ok(RgbImage { width: rgb.width() as usize, height: rgb.height() as usize, pixels: rgb.into_raw() })
    }
}
