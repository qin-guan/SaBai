namespace SaBai.API.FaceRecognition;

/// <summary>Bounded detection retries; recognition always samples pixels from the original photo.</summary>
internal static class BestEffortFaceDetection
{
    public static IReadOnlyList<DetectedFace> Detect(
        RgbImage image,
        FaceRecognitionOptions options,
        Func<RgbImage, int, float, float, IReadOnlyList<DetectedFace>> detect,
        CancellationToken ct
    )
    {
        var geometryOnly = new FaceRecognitionOptions();
        IReadOnlyList<DetectedFace> Run(RgbImage input, int size, float threshold)
        {
            ct.ThrowIfCancellationRequested();
            // Reject invalid geometry before it can suppress usable detections across passes.
            return detect(input, size, threshold, options.NmsThreshold)
                .Where(f => FaceQuality.IsSuitable(f, geometryOnly))
                .ToList();
        }

        var primary = Run(image, options.DetectionSize, options.DetectionThreshold);
        if (
            !options.EnableDetectionFallback
            || (primary.Count > 0 && primary.All(f => Math.Min(f.Box.Width, f.Box.Height) >= 64))
        )
            return primary;

        var size = Math.Max(options.DetectionSize, options.DetectionFallbackSize);
        var retry = Run(image, size, options.DetectionFallbackThreshold);
        // Keep existing primary landmarks, rather than replacing them with a different pass.
        var merged = Merge(primary, retry, options.NmsThreshold);
        if (merged.Count > 0)
            return merged;

        var brightened = ImageOps.BrightenShadows(image);
        var lighting = Run(brightened, size, options.DetectionFallbackThreshold);
        if (lighting.Count > 0)
            return lighting;

        // Roll retries are only used after unrotated passes fail. They do not synthesize
        // hidden facial features or compensate for a full profile/yaw angle.
        var rotatedFaces = new List<DetectedFace>();
        foreach (var degrees in new[] { -25.0, 25.0 })
        {
            ct.ThrowIfCancellationRequested();
            var rotated = ImageOps.Rotate(brightened, degrees);
            var inverse = rotated.Transform.Inverse();
            rotatedFaces.AddRange(
                Run(rotated.Image, size, options.DetectionFallbackThreshold)
                    .Select(f => MapToOriginal(f, inverse, image.Width, image.Height))
                    .Where(f => FaceQuality.IsSuitable(f, geometryOnly))
            );
        }

        return ScrfdFaceDetector.NonMaxSuppression(rotatedFaces, options.NmsThreshold);
    }

    internal static IReadOnlyList<DetectedFace> Merge(
        IReadOnlyList<DetectedFace> primary,
        IReadOnlyList<DetectedFace> retry,
        float nmsThreshold
    )
    {
        var additional = retry.Where(f => primary.All(p => p.Box.IoU(f.Box) <= nmsThreshold));
        return primary
            .Concat(ScrfdFaceDetector.NonMaxSuppression(additional, nmsThreshold))
            .OrderByDescending(f => f.Score)
            .ToList();
    }

    internal static DetectedFace MapToOriginal(
        DetectedFace face,
        SimilarityTransform inverse,
        int width,
        int height
    )
    {
        var box = face.Box;
        var corners = new[]
        {
            inverse.Apply(box.X1, box.Y1),
            inverse.Apply(box.X2, box.Y1),
            inverse.Apply(box.X1, box.Y2),
            inverse.Apply(box.X2, box.Y2),
        };
        return face with
        {
            Box = new BoundingBox(
                Math.Clamp(corners.Min(p => p.X), 0, width),
                Math.Clamp(corners.Min(p => p.Y), 0, height),
                Math.Clamp(corners.Max(p => p.X), 0, width),
                Math.Clamp(corners.Max(p => p.Y), 0, height)
            ),
            Landmarks = face
                .Landmarks.Select(p => inverse.Apply(p.X, p.Y))
                .Select(p => new PointF(p.X, p.Y))
                .ToArray(),
        };
    }
}
