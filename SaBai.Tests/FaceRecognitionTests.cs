using SaBai.API.FaceRecognition;
using SkiaSharp;

namespace SaBai.Tests;

public class SimilarityTransformTests
{
    [Test]
    public async Task Estimate_RecoversKnownTransform()
    {
        var expected = new SimilarityTransform(0.8 * Math.Cos(0.3), 0.8 * Math.Sin(0.3), 12, -7);
        var src = new PointF[] { new(10, 20), new(60, 25), new(35, 50), new(15, 80), new(55, 82) };
        var dst = src.Select(p => expected.Apply(p.X, p.Y))
            .Select(p => new PointF(p.X, p.Y))
            .ToArray();

        var actual = SimilarityTransform.Estimate(src, dst);

        await Assert.That(actual.A).IsEqualTo(expected.A).Within(1e-5);
        await Assert.That(actual.B).IsEqualTo(expected.B).Within(1e-5);
        await Assert.That(actual.Tx).IsEqualTo(expected.Tx).Within(1e-3);
        await Assert.That(actual.Ty).IsEqualTo(expected.Ty).Within(1e-3);
    }

    [Test]
    public async Task Inverse_UndoesTransform()
    {
        var t = new SimilarityTransform(1.3, -0.4, 5, 9);
        var (x, y) = t.Apply(17, 42);
        var (bx, by) = t.Inverse().Apply(x, y);

        await Assert.That(bx).IsEqualTo(17f).Within(1e-3f);
        await Assert.That(by).IsEqualTo(42f).Within(1e-3f);
    }

    [Test]
    public async Task Align_WithTemplateLandmarks_IsIdentityCrop()
    {
        var image = new RgbImage(FaceAligner.Size, FaceAligner.Size);
        new Random(42).NextBytes(image.Pixels);

        var aligned = FaceAligner.Align(image, FaceAligner.ArcFaceTemplate);

        await Assert.That(aligned.Pixels.SequenceEqual(image.Pixels)).IsTrue();
    }
}

public class DetectionTests
{
    [Test]
    public async Task IoU_ComputesOverlap()
    {
        var a = new BoundingBox(0, 0, 10, 10);
        var b = new BoundingBox(5, 0, 15, 10);

        await Assert.That(a.IoU(b)).IsEqualTo(50f / 150f).Within(1e-6f);
        await Assert.That(a.IoU(new BoundingBox(20, 20, 30, 30))).IsEqualTo(0f);
    }

    [Test]
    public async Task NonMaxSuppression_KeepsHighestScoringOfOverlappingBoxes()
    {
        var faces = new[]
        {
            new DetectedFace(new BoundingBox(0, 0, 10, 10), 0.7f, []),
            new DetectedFace(new BoundingBox(1, 1, 11, 11), 0.9f, []),
            new DetectedFace(new BoundingBox(50, 50, 60, 60), 0.6f, []),
        };

        var kept = ScrfdFaceDetector.NonMaxSuppression(faces, 0.4f);

        await Assert.That(kept.Select(f => f.Score).ToArray()).IsEquivalentTo(new[] { 0.9f, 0.6f });
    }
}

public class ImageOpsTests
{
    [Test]
    public async Task ResizeBilinear_ShrinkUsesHalfPixelCoordinates()
    {
        var image = new RgbImage(4, 1, [0, 0, 0, 100, 100, 100, 200, 200, 200, 240, 240, 240]);

        var resized = ImageOps.ResizeBilinear(image, 2, 1);

        await Assert
            .That(resized.Pixels.SequenceEqual(new byte[] { 50, 50, 50, 220, 220, 220 }))
            .IsTrue();
    }

    [Test]
    public async Task BrightenShadows_PreservesBlackAndWhiteAndBoundsGain()
    {
        var image = new RgbImage(3, 1, [0, 0, 0, 255, 255, 255, 10, 10, 10]);

        var brightened = ImageOps.BrightenShadows(image);

        await Assert.That(brightened.Pixels.Take(6).SequenceEqual(image.Pixels.Take(6))).IsTrue();
        await Assert.That(brightened.Pixels[6] > 10 && brightened.Pixels[6] <= 30).IsTrue();
        await Assert.That(image.Pixels[6]).IsEqualTo((byte)10);
    }

    [Test]
    public async Task Resize_FractionalShrink_WeightsPartialPixels()
    {
        var image = new RgbImage(
            3,
            2,
            [0, 0, 0, 120, 120, 120, 240, 240, 240, 0, 0, 0, 120, 120, 120, 240, 240, 240]
        );

        var resized = ImageOps.Resize(image, 2, 1);

        // Each output covers 1.5 columns: a whole edge pixel and half the middle pixel.
        await Assert
            .That(resized.Pixels.SequenceEqual(new byte[] { 40, 40, 40, 200, 200, 200 }))
            .IsTrue();
    }

    [Test]
    public async Task SampleBilinear_InterpolatesChannelsIndependently()
    {
        var image = new RgbImage(2, 1, [20, 40, 60, 100, 120, 140]);
        var sample = new byte[3];

        ImageOps.SampleBilinear(image, 0.5f, 0, sample);

        await Assert.That(sample.SequenceEqual(new byte[] { 60, 80, 100 })).IsTrue();
    }

    [Test]
    public async Task SampleBilinear_OutsideImage_IsBlack()
    {
        var image = new RgbImage(1, 1, [255, 255, 255]);
        var sample = new byte[] { 255, 255, 255 };

        ImageOps.SampleBilinear(image, -2, -2, sample);

        await Assert.That(sample.SequenceEqual(new byte[] { 0, 0, 0 })).IsTrue();
    }
}

public class FaceQualityTests
{
    private static DetectedFace Face() =>
        new(new BoundingBox(0, 0, 112, 112), 0.9f, FaceAligner.ArcFaceTemplate.ToArray());

    [Test]
    public async Task IsSuitable_AcceptsWellResolvedFace()
    {
        await Assert.That(FaceQuality.IsSuitable(Face(), new FaceRecognitionOptions())).IsTrue();
    }

    [Test]
    public async Task IsSuitable_RejectsSmallFace()
    {
        var face = Face() with { Box = new BoundingBox(0, 0, 39, 112) };

        await Assert
            .That(FaceQuality.IsSuitable(face, new FaceRecognitionOptions { MinimumFaceSize = 40 }))
            .IsFalse();
    }

    [Test]
    public async Task IsSuitable_RejectsInsufficientEyeResolution()
    {
        var face = Face();
        face.Landmarks[1] = face.Landmarks[0] with { X = face.Landmarks[0].X + 5 };

        await Assert
            .That(
                FaceQuality.IsSuitable(face, new FaceRecognitionOptions { MinimumEyeDistance = 12 })
            )
            .IsFalse();
    }

    [Test]
    public async Task IsSuitable_RejectsNonFiniteLandmarks()
    {
        var face = Face();
        face.Landmarks[2] = new PointF(float.NaN, 50);

        await Assert.That(FaceQuality.IsSuitable(face, new FaceRecognitionOptions())).IsFalse();
    }

    [Test]
    public async Task IsSuitable_RejectsDegenerateLandmarksEvenWithSizeGuardsDisabled()
    {
        var face = Face() with { Landmarks = Enumerable.Repeat(new PointF(50, 50), 5).ToArray() };
        var options = new FaceRecognitionOptions { MinimumFaceSize = 0, MinimumEyeDistance = 0 };

        await Assert.That(FaceQuality.IsSuitable(face, options)).IsFalse();
    }

    [Test]
    public async Task IsSuitable_SizeGuardsCanBeDisabled()
    {
        var face = Face() with
        {
            Box = new BoundingBox(0, 0, 20, 20),
            Landmarks = FaceAligner
                .ArcFaceTemplate.Select(p => new PointF(p.X / 10, p.Y / 10))
                .ToArray(),
        };
        var options = new FaceRecognitionOptions { MinimumFaceSize = 0, MinimumEyeDistance = 0 };

        await Assert.That(FaceQuality.IsSuitable(face, options)).IsTrue();
    }

    [Test]
    public async Task IsSuitable_DefaultsAcceptSmallAngledFace()
    {
        var transform = new SimilarityTransform(0.15, 0.1, 2, 1);
        var face = Face() with
        {
            Box = new BoundingBox(0, 0, 24, 24),
            Landmarks = FaceAligner
                .ArcFaceTemplate.Select(p => transform.Apply(p.X, p.Y))
                .Select(p => new PointF(p.X, p.Y))
                .ToArray(),
        };

        await Assert.That(FaceQuality.IsSuitable(face, new FaceRecognitionOptions())).IsTrue();
    }
}

public class BestEffortFaceDetectionTests
{
    private static DetectedFace Face(float score = 0.9f) =>
        new(new BoundingBox(0, 0, 112, 112), score, FaceAligner.ArcFaceTemplate.ToArray());

    [Test]
    public async Task Detect_GoodPrimaryDetectionDoesNotRetry()
    {
        var calls = 0;
        IReadOnlyList<DetectedFace> Detect(RgbImage image, int size, float threshold, float nms)
        {
            calls++;
            return [Face()];
        }

        var result = BestEffortFaceDetection.Detect(
            new RgbImage(112, 112),
            new FaceRecognitionOptions(),
            Detect,
            CancellationToken.None
        );

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Detect_MissedFaceRetriesAtHigherResolutionWithoutChangingMatchThreshold()
    {
        var calls = 0;
        var lastSize = 0;
        var lastThreshold = 0f;
        IReadOnlyList<DetectedFace> Detect(RgbImage image, int size, float threshold, float nms)
        {
            calls++;
            lastSize = size;
            lastThreshold = threshold;
            return calls == 1 ? [] : [Face(0.4f)];
        }

        var options = new FaceRecognitionOptions();
        var result = BestEffortFaceDetection.Detect(
            new RgbImage(112, 112),
            options,
            Detect,
            CancellationToken.None
        );

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(lastSize).IsEqualTo(1024);
        await Assert.That(lastThreshold).IsEqualTo(0.35f);
        await Assert.That(options.MatchThreshold).IsEqualTo(0.4f);
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Detect_LightingRetryDoesNotModifyOriginalPixels()
    {
        var source = new RgbImage(112, 112);
        Array.Fill(source.Pixels, (byte)20);
        var calls = 0;
        byte retryPixel = 0;
        IReadOnlyList<DetectedFace> Detect(RgbImage image, int size, float threshold, float nms)
        {
            calls++;
            retryPixel = image.Pixels[0];
            return calls < 3 ? [] : [Face()];
        }

        var result = BestEffortFaceDetection.Detect(
            source,
            new FaceRecognitionOptions(),
            Detect,
            CancellationToken.None
        );

        await Assert.That(calls).IsEqualTo(3);
        await Assert.That(retryPixel > 20).IsTrue();
        await Assert.That(source.Pixels.All(p => p == 20)).IsTrue();
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Detect_FallbackCanBeDisabled()
    {
        var calls = 0;
        IReadOnlyList<DetectedFace> Detect(RgbImage image, int size, float threshold, float nms)
        {
            calls++;
            return [];
        }

        var result = BestEffortFaceDetection.Detect(
            new RgbImage(112, 112),
            new FaceRecognitionOptions { EnableDetectionFallback = false },
            Detect,
            CancellationToken.None
        );

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Detect_NoFaceUsesAtMostFivePasses()
    {
        var calls = 0;
        IReadOnlyList<DetectedFace> Detect(RgbImage image, int size, float threshold, float nms)
        {
            calls++;
            return [];
        }

        var result = BestEffortFaceDetection.Detect(
            new RgbImage(112, 112),
            new FaceRecognitionOptions(),
            Detect,
            CancellationToken.None
        );

        await Assert.That(calls).IsEqualTo(5);
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Detect_SmallPrimaryFaceIsKeptDuringRetry()
    {
        var calls = 0;
        var small = Face(0.6f) with { Box = new BoundingBox(0, 0, 24, 24) };
        IReadOnlyList<DetectedFace> Detect(RgbImage image, int size, float threshold, float nms)
        {
            calls++;
            return calls == 1 ? [small] : [];
        }

        var result = BestEffortFaceDetection.Detect(
            new RgbImage(112, 112),
            new FaceRecognitionOptions(),
            Detect,
            CancellationToken.None
        );

        await Assert.That(calls).IsEqualTo(2);
        await Assert.That(ReferenceEquals(result[0], small)).IsTrue();
    }

    [Test]
    public async Task Merge_PreservesPrimaryLandmarksWhenRetryOverlaps()
    {
        var primary = Face(0.6f);
        var retry = Face(0.9f) with { Box = new BoundingBox(1, 1, 111, 111) };

        var result = BestEffortFaceDetection.Merge([primary], [retry], 0.4f);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(ReferenceEquals(result[0], primary)).IsTrue();
    }

    [Test]
    public async Task MapToOriginal_RestoresLandmarksAfterRotation()
    {
        var original = new RgbImage(112, 112);
        var rotation = ImageOps.Rotate(original, 25);
        var face = Face() with
        {
            Box = new BoundingBox(0, 0, rotation.Image.Width, rotation.Image.Height),
            Landmarks = FaceAligner
                .ArcFaceTemplate.Select(p => rotation.Transform.Apply(p.X, p.Y))
                .Select(p => new PointF(p.X, p.Y))
                .ToArray(),
        };

        var mapped = BestEffortFaceDetection.MapToOriginal(
            face,
            rotation.Transform.Inverse(),
            112,
            112
        );

        for (var i = 0; i < 5; i++)
        {
            await Assert
                .That(mapped.Landmarks[i].X)
                .IsEqualTo(FaceAligner.ArcFaceTemplate[i].X)
                .Within(1e-3f);
            await Assert
                .That(mapped.Landmarks[i].Y)
                .IsEqualTo(FaceAligner.ArcFaceTemplate[i].Y)
                .Within(1e-3f);
        }
        await Assert.That(mapped.Box.X1).IsEqualTo(0f);
        await Assert.That(mapped.Box.X2).IsEqualTo(112f);
    }
}

public class FaceReferenceHistoryTests
{
    [Test]
    public async Task BestMatches_UsesStrongestReferenceWithoutAveragingDifferentAges()
    {
        var oldPhoto = new FaceMatch(Guid.NewGuid(), "child-a", 0.45f);
        var recentPhoto = new FaceMatch(Guid.NewGuid(), "child-a", 0.72f);
        var otherChild = new FaceMatch(Guid.NewGuid(), "child-b", 0.61f);

        var result = FaceStore.BestMatches([oldPhoto, recentPhoto, otherChild], 2);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].FaceId).IsEqualTo(recentPhoto.FaceId);
        await Assert.That(result[0].Similarity).IsEqualTo(0.72f);
        await Assert.That(result[1].PersonId).IsEqualTo("child-b");
    }
}

public class RgbImageTests
{
    // 2x3 image whose red channel encodes the pixel position as 10 * y + x.
    private static RgbImage Sample()
    {
        var image = new RgbImage(2, 3);
        for (var y = 0; y < 3; y++)
        for (var x = 0; x < 2; x++)
            image.Pixels[(y * 2 + x) * 3] = (byte)(10 * y + x);
        return image;
    }

    private static byte At(RgbImage image, int x, int y) => image.Pixels[(y * image.Width + x) * 3];

    [Test]
    public async Task Orient_RightTop_RotatesClockwise()
    {
        var rotated = RgbImage.Orient(Sample(), SKEncodedOrigin.RightTop);

        await Assert.That(rotated.Width).IsEqualTo(3);
        await Assert.That(rotated.Height).IsEqualTo(2);
        // The bottom-left source pixel ends up top-left.
        await Assert.That(At(rotated, 0, 0)).IsEqualTo((byte)20);
        await Assert.That(At(rotated, 2, 0)).IsEqualTo((byte)0);
        await Assert.That(At(rotated, 2, 1)).IsEqualTo((byte)1);
    }

    [Test]
    public async Task Orient_LeftBottom_RotatesCounterClockwise()
    {
        var rotated = RgbImage.Orient(Sample(), SKEncodedOrigin.LeftBottom);

        // The top-right source pixel ends up top-left.
        await Assert.That(At(rotated, 0, 0)).IsEqualTo((byte)1);
        await Assert.That(At(rotated, 0, 1)).IsEqualTo((byte)0);
        await Assert.That(At(rotated, 2, 1)).IsEqualTo((byte)20);
    }

    [Test]
    public async Task Decode_PngRoundTrip()
    {
        using var bitmap = new SKBitmap(4, 2);
        bitmap.Erase(new SKColor(10, 20, 30));
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        var image = RgbImage.Decode(png.AsStream());

        await Assert.That(image.Width).IsEqualTo(4);
        await Assert.That(image.Pixels.Take(3).ToArray()).IsEquivalentTo(new byte[] { 10, 20, 30 });
    }

    [Test]
    public async Task Decode_RejectsGarbage()
    {
        await Assert
            .That(() => RgbImage.Decode(new MemoryStream("not an image"u8.ToArray())))
            .Throws<InvalidImageException>();
    }
}
