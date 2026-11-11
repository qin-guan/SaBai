package sabai.api.facerecognition

import com.drew.imaging.ImageMetadataReader
import com.drew.metadata.exif.ExifIFD0Directory
import java.io.ByteArrayInputStream
import javax.imageio.ImageIO

/** Tightly packed 8-bit RGB image. */
class RgbImage(val width: Int, val height: Int, val pixels: ByteArray = ByteArray(width * height * 3)) {
    init {
        require(pixels.size == width * height * 3) { "Pixel buffer does not match dimensions." }
    }

    companion object {
        /** Decodes JPEG/PNG/BMP/GIF and applies the EXIF orientation. (ImageIO has no WebP decoder.) */
        fun decode(bytes: ByteArray): RgbImage {
            val image =
                runCatching { ImageIO.read(ByteArrayInputStream(bytes)) }.getOrNull()
                    ?: throw InvalidImageException("Unsupported or corrupt image.")
            val argb = image.getRGB(0, 0, image.width, image.height, null, 0, image.width)
            val rgb = RgbImage(image.width, image.height)
            for (i in argb.indices) {
                rgb.pixels[i * 3] = (argb[i] shr 16).toByte()
                rgb.pixels[i * 3 + 1] = (argb[i] shr 8).toByte()
                rgb.pixels[i * 3 + 2] = argb[i].toByte()
            }
            return orient(rgb, exifOrientation(bytes))
        }

        private fun exifOrientation(bytes: ByteArray): Int =
            runCatching {
                ImageMetadataReader.readMetadata(ByteArrayInputStream(bytes))
                    .getFirstDirectoryOfType(ExifIFD0Directory::class.java)
                    ?.getInteger(ExifIFD0Directory.TAG_ORIENTATION)
            }.getOrNull() ?: 1

        /** Remaps pixels so the image is upright for its EXIF orientation (1-8). */
        fun orient(image: RgbImage, orientation: Int): RgbImage {
            val w = image.width
            val h = image.height
            val map: ((Int, Int) -> Pair<Int, Int>) =
                when (orientation) {
                    2 -> { x, y -> (w - 1 - x) to y }
                    3 -> { x, y -> (w - 1 - x) to (h - 1 - y) }
                    4 -> { x, y -> x to (h - 1 - y) }
                    5 -> { x, y -> y to x }
                    6 -> { x, y -> y to (h - 1 - x) }
                    7 -> { x, y -> (w - 1 - y) to (h - 1 - x) }
                    8 -> { x, y -> (w - 1 - y) to x }
                    else -> return image
                }
            val swap = orientation >= 5
            val result = RgbImage(if (swap) h else w, if (swap) w else h)
            for (y in 0 until result.height) {
                for (x in 0 until result.width) {
                    val (sx, sy) = map(x, y)
                    System.arraycopy(image.pixels, (sy * w + sx) * 3, result.pixels, (y * result.width + x) * 3, 3)
                }
            }
            return result
        }
    }
}
