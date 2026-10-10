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
        Accumulate(image, x0, y0, (1 - fx) * (1 - fy), acc);
        Accumulate(image, x0 + 1, y0, fx * (1 - fy), acc);
        Accumulate(image, x0, y0 + 1, (1 - fx) * fy, acc);
        Accumulate(image, x0 + 1, y0 + 1, fx * fy, acc);

        for (var c = 0; c < 3; c++)
            rgb[c] = (byte)Math.Clamp(MathF.Round(acc[c]), 0, 255);
    }

    private static void Accumulate(RgbImage image, int x, int y, float weight, Span<float> acc)
    {
        if (weight == 0 || x < 0 || y < 0 || x >= image.Width || y >= image.Height) return;
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
        if (width == image.Width && height == image.Height) return image;

        var output = new RgbImage(width, height);
        var scaleX = (float)image.Width / width;
        var scaleY = (float)image.Height / height;

        if (scaleX >= 1 && scaleY >= 1)
        {
            for (var y = 0; y < height; y++)
            {
                var sy0 = (int)(y * scaleY);
                var sy1 = Math.Max(sy0 + 1, Math.Min(image.Height, (int)MathF.Ceiling((y + 1) * scaleY)));
                for (var x = 0; x < width; x++)
                {
                    var sx0 = (int)(x * scaleX);
                    var sx1 = Math.Max(sx0 + 1, Math.Min(image.Width, (int)MathF.Ceiling((x + 1) * scaleX)));
                    int r = 0, g = 0, b = 0;
                    for (var sy = sy0; sy < sy1; sy++)
                    {
                        var i = (sy * image.Width + sx0) * 3;
                        for (var sx = sx0; sx < sx1; sx++, i += 3)
                        {
                            r += image.Pixels[i];
                            g += image.Pixels[i + 1];
                            b += image.Pixels[i + 2];
                        }
                    }

                    var n = (sy1 - sy0) * (sx1 - sx0);
                    var o = (y * width + x) * 3;
                    output.Pixels[o] = (byte)((r + n / 2) / n);
                    output.Pixels[o + 1] = (byte)((g + n / 2) / n);
                    output.Pixels[o + 2] = (byte)((b + n / 2) / n);
                }
            }

            return output;
        }

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
}
