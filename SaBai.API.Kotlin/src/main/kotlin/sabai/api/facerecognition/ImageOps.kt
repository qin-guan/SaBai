package sabai.api.facerecognition

import kotlin.math.abs
import kotlin.math.ceil
import kotlin.math.cos
import kotlin.math.floor
import kotlin.math.min
import kotlin.math.pow
import kotlin.math.roundToInt
import kotlin.math.sin

internal object ImageOps {
    /** Bilinear sample at (x, y); out-of-bounds neighbours read as black. Writes 3 bytes at [offset]. */
    fun sampleBilinear(image: RgbImage, x: Float, y: Float, out: ByteArray, offset: Int) {
        val x0 = floor(x).toInt()
        val y0 = floor(y).toInt()
        val fx = x - x0
        val fy = y - y0
        val acc = FloatArray(3)
        accumulate(image, x0, y0, (1 - fx) * (1 - fy), acc)
        accumulate(image, x0 + 1, y0, fx * (1 - fy), acc)
        accumulate(image, x0, y0 + 1, (1 - fx) * fy, acc)
        accumulate(image, x0 + 1, y0 + 1, fx * fy, acc)
        for (c in 0..2) out[offset + c] = acc[c].roundToInt().coerceIn(0, 255).toByte()
    }

    private fun accumulate(image: RgbImage, x: Int, y: Int, weight: Float, acc: FloatArray) {
        if (weight == 0f || x < 0 || y < 0 || x >= image.width || y >= image.height) return
        val i = (y * image.width + x) * 3
        for (c in 0..2) acc[c] += (image.pixels[i + c].toInt() and 0xff) * weight
    }

    /** Half-pixel bilinear resize, as in the SCRFD reference pipeline. */
    fun resizeBilinear(image: RgbImage, width: Int, height: Int): RgbImage {
        if (width == image.width && height == image.height) return image
        val output = RgbImage(width, height)
        val scaleX = image.width.toFloat() / width
        val scaleY = image.height.toFloat() / height
        for (y in 0 until height) {
            val sy = ((y + 0.5f) * scaleY - 0.5f).coerceIn(0f, (image.height - 1).toFloat())
            for (x in 0 until width) {
                val sx = ((x + 0.5f) * scaleX - 0.5f).coerceIn(0f, (image.width - 1).toFloat())
                sampleBilinear(image, sx, sy, output.pixels, (y * width + x) * 3)
            }
        }
        return output
    }

    /** Lift shadows for detection only, keeping RGB ratios and limiting noise amplification. */
    fun brightenShadows(image: RgbImage): RgbImage {
        val output = RgbImage(image.width, image.height)
        val p = image.pixels
        for (i in p.indices step 3) {
            val r = p[i].toInt() and 0xff
            val g = p[i + 1].toInt() and 0xff
            val b = p[i + 2].toInt() and 0xff
            val luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255
            val gain = if (luminance > 0) min(3.0, luminance.pow(-0.35)) else 1.0
            output.pixels[i] = (r * gain).roundToInt().coerceIn(0, 255).toByte()
            output.pixels[i + 1] = (g * gain).roundToInt().coerceIn(0, 255).toByte()
            output.pixels[i + 2] = (b * gain).roundToInt().coerceIn(0, 255).toByte()
        }
        return output
    }

    /** Rotate on an expanded canvas. The transform maps original to rotated coordinates. */
    fun rotate(image: RgbImage, degrees: Double): Pair<RgbImage, SimilarityTransform> {
        val angle = Math.toRadians(degrees)
        val a = cos(angle)
        val b = sin(angle)
        val width = ceil(abs(a) * (image.width - 1) + abs(b) * (image.height - 1)).toInt() + 1
        val height = ceil(abs(b) * (image.width - 1) + abs(a) * (image.height - 1)).toInt() + 1
        val cx = (image.width - 1) / 2.0
        val cy = (image.height - 1) / 2.0
        val transform =
            SimilarityTransform(a, b, (width - 1) / 2.0 - a * cx + b * cy, (height - 1) / 2.0 - b * cx - a * cy)
        val inverse = transform.inverse()
        val output = RgbImage(width, height)
        for (y in 0 until height) {
            for (x in 0 until width) {
                val (sx, sy) = inverse.apply(x.toDouble(), y.toDouble())
                sampleBilinear(image, sx, sy, output.pixels, (y * width + x) * 3)
            }
        }
        return output to transform
    }
}
