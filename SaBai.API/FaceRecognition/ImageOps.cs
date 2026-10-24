namespace SaBai.API.FaceRecognition;

internal static class ImageOps
{
    /// <summary>Bilinear sample at (x, y) in pixel coordinates; out-of-bounds neighbours read as black.</summary>
    public static void SampleBilinear(RgbImage image, float x, float y, Span<byte> rgb)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;

        Span<float> acc = stackalloc float[3];
        acc.Clear();
        Accumulate(image, x0, y0, (1 - fx) * (1 - fy), acc);
        Accumulate(image, x0 + 1, y0, fx * (1 - fy), acc);
        Accumulate(image, x0, y0 + 1, (1 - fx) * fy, acc);
        Accumulate(image, x0 + 1, y0 + 1, fx * fy, acc);

        for (var c = 0; c < 3; c++)
            rgb[c] = (byte)Math.Clamp(MathF.Round(acc[c]), 0, 255);
    }

    private static void Accumulate(RgbImage image, int x, int y, float weight, Span<float> acc)
    {
        if (weight == 0 || x < 0 || y < 0 || x >= image.Width || y >= image.Height)
            return;
        var i = (y * image.Width + x) * 3;
        acc[0] += image.Pixels[i] * weight;
        acc[1] += image.Pixels[i + 1] * weight;
        acc[2] += image.Pixels[i + 2] * weight;
    }

    /// <summary>
    /// Resizes an image. Uses area averaging when shrinking (avoids aliasing on large photos)
    /// and bilinear interpolation when enlarging.
    /// </summary>
    public static RgbImage Resize(RgbImage image, int width, int height)
    {
        if (width == image.Width && height == image.Height)
            return image;

        var output = new RgbImage(width, height);
        var scaleX = (float)image.Width / width;
        var scaleY = (float)image.Height / height;

        if (scaleX >= 1 && scaleY >= 1)
        {
            for (var y = 0; y < height; y++)
            {
                var sy0 = (int)(y * scaleY);
                var sy1 = Math.Max(
                    sy0 + 1,
                    Math.Min(image.Height, (int)MathF.Ceiling((y + 1) * scaleY))
                );
                for (var x = 0; x < width; x++)
                {
                    var sx0 = (int)(x * scaleX);
                    var sx1 = Math.Max(
                        sx0 + 1,
                        Math.Min(image.Width, (int)MathF.Ceiling((x + 1) * scaleX))
                    );
                    double r = 0,
                        g = 0,
                        b = 0,
                        totalWeight = 0;
                    for (var sy = sy0; sy < sy1; sy++)
                    {
                        var wy = MathF.Min(sy + 1, (y + 1) * scaleY) - MathF.Max(sy, y * scaleY);
                        var i = (sy * image.Width + sx0) * 3;
                        for (var sx = sx0; sx < sx1; sx++, i += 3)
                        {
                            var wx =
                                MathF.Min(sx + 1, (x + 1) * scaleX) - MathF.Max(sx, x * scaleX);
                            var weight = MathF.Max(0, wx) * MathF.Max(0, wy);
                            r += image.Pixels[i] * weight;
                            g += image.Pixels[i + 1] * weight;
                            b += image.Pixels[i + 2] * weight;
                            totalWeight += weight;
                        }
                    }

                    var o = (y * width + x) * 3;
                    output.Pixels[o] = (byte)Math.Clamp(Math.Round(r / totalWeight), 0, 255);
                    output.Pixels[o + 1] = (byte)Math.Clamp(Math.Round(g / totalWeight), 0, 255);
                    output.Pixels[o + 2] = (byte)Math.Clamp(Math.Round(b / totalWeight), 0, 255);
                }
            }

            return output;
        }

        return ResizeBilinear(image, width, height);
    }

    /// <summary>Half-pixel bilinear resize, including when shrinking, as used by the SCRFD reference pipeline.</summary>
    public static RgbImage ResizeBilinear(RgbImage image, int width, int height)
    {
        if (width == image.Width && height == image.Height)
            return image;
        var output = new RgbImage(width, height);
        var scaleX = (float)image.Width / width;
        var scaleY = (float)image.Height / height;
        for (var y = 0; y < height; y++)
        {
            var sy = Math.Clamp((y + 0.5f) * scaleY - 0.5f, 0, image.Height - 1);
            for (var x = 0; x < width; x++)
            {
                var sx = Math.Clamp((x + 0.5f) * scaleX - 0.5f, 0, image.Width - 1);
                SampleBilinear(image, sx, sy, output.Pixels.AsSpan((y * width + x) * 3, 3));
            }
        }

        return output;
    }

    /// <summary>Lift shadows for detection only, retaining RGB ratios and limiting noise amplification.</summary>
    public static RgbImage BrightenShadows(RgbImage image)
    {
        var output = new RgbImage(image.Width, image.Height);
        for (var i = 0; i < image.Pixels.Length; i += 3)
        {
            var luminance =
                (
                    0.2126 * image.Pixels[i]
                    + 0.7152 * image.Pixels[i + 1]
                    + 0.0722 * image.Pixels[i + 2]
                ) / 255;
            var gain = luminance > 0 ? Math.Min(3, Math.Pow(luminance, -0.35)) : 1;
            for (var c = 0; c < 3; c++)
                output.Pixels[i + c] = (byte)
                    Math.Clamp(Math.Round(image.Pixels[i + c] * gain), 0, 255);
        }

        return output;
    }

    /// <summary>Rotate on an expanded canvas. The returned transform maps original to rotated coordinates.</summary>
    public static (RgbImage Image, SimilarityTransform Transform) Rotate(
        RgbImage image,
        double degrees
    )
    {
        var angle = degrees * Math.PI / 180;
        var a = Math.Cos(angle);
        var b = Math.Sin(angle);
        var width =
            (int)Math.Ceiling(Math.Abs(a) * (image.Width - 1) + Math.Abs(b) * (image.Height - 1))
            + 1;
        var height =
            (int)Math.Ceiling(Math.Abs(b) * (image.Width - 1) + Math.Abs(a) * (image.Height - 1))
            + 1;
        var cx = (image.Width - 1) / 2.0;
        var cy = (image.Height - 1) / 2.0;
        var transform = new SimilarityTransform(
            a,
            b,
            (width - 1) / 2.0 - a * cx + b * cy,
            (height - 1) / 2.0 - b * cx - a * cy
        );
        var inverse = transform.Inverse();
        var output = new RgbImage(width, height);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var (sx, sy) = inverse.Apply(x, y);
            SampleBilinear(image, sx, sy, output.Pixels.AsSpan((y * width + x) * 3, 3));
        }

        return (output, transform);
    }
}
