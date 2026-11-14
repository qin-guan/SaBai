namespace SaBai.API.FaceRecognition;

public sealed class FaceRecognitionOptions
{
    public const string SectionName = "FaceRecognition";

    /// <summary>Directory holding the ONNX models. Defaults to a per-user data directory.</summary>
    public string ModelsDirectory { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create
            ),
            "sabai",
            "models"
        );

    /// <summary>File name of the SCRFD face detection model inside <see cref="ModelsDirectory"/>.</summary>
    public string DetectorModel { get; set; } = "det_10g.onnx";

    /// <summary>File name of the ArcFace recognition model inside <see cref="ModelsDirectory"/>.</summary>
    public string RecognizerModel { get; set; } = "w600k_r50.onnx";

    /// <summary>
    /// Expected SHA-256 (hex) of <see cref="DetectorModel"/>; the file is checked before it is loaded.
    /// Defaults to buffalo_l's <c>det_10g.onnx</c>. Required when <see cref="AutoDownloadModels"/> is on;
    /// empty skips the check for operator-provided files.
    /// </summary>
    public string DetectorModelSha256 { get; set; } =
        "5838f7fe053675b1c7a08b633df49e7af5495cee0493c7dcf6697200b85b5b91";

    /// <summary>
    /// Expected SHA-256 (hex) of <see cref="RecognizerModel"/>; the file is checked before it is loaded.
    /// Defaults to buffalo_l's <c>w600k_r50.onnx</c>. Required when <see cref="AutoDownloadModels"/> is on;
    /// empty skips the check for operator-provided files.
    /// </summary>
    public string RecognizerModelSha256 { get; set; } =
        "4c06341c33c2ca1f86781dab0e829f88ad5b64be9fba56e56bc9ebdefc619e43";

    /// <summary>Download missing models from <see cref="ModelPackUrl"/> on first use.</summary>
    public bool AutoDownloadModels { get; set; } = true;

    /// <summary>Zip archive containing both models (InsightFace buffalo_l pack by default).</summary>
    public string ModelPackUrl { get; set; } =
        "https://github.com/deepinsight/insightface/releases/download/v0.7/buffalo_l.zip";

    /// <summary>Square input size the detector runs at.</summary>
    public int DetectionSize { get; set; } = 640;

    /// <summary>Minimum detector confidence for a face to be kept.</summary>
    public float DetectionThreshold { get; set; } = 0.5f;

    /// <summary>Retry missed or small faces at higher resolution, with shadow lifting and roll correction.</summary>
    public bool EnableDetectionFallback { get; set; } = true;

    /// <summary>Square detector size for best-effort retries; fixed-size ONNX exports retain their declared size.</summary>
    public int DetectionFallbackSize { get; set; } = 1024;

    /// <summary>Detector confidence for retries only. This does not lower the identity-match threshold.</summary>
    public float DetectionFallbackThreshold { get; set; } = 0.35f;

    /// <summary>IoU threshold for non-maximum suppression.</summary>
    public float NmsThreshold { get; set; } = 0.4f;

    /// <summary>Minimum face width and height in source pixels for recognition; zero disables the size guard.</summary>
    public int MinimumFaceSize { get; set; }

    /// <summary>Minimum distance between the eyes in source pixels; zero disables the resolution guard.</summary>
    public float MinimumEyeDistance { get; set; }

    /// <summary>Cosine similarity at or above which two faces are considered the same person.</summary>
    public float MatchThreshold { get; set; } = 0.4f;

    /// <summary>Qdrant collection storing face embeddings.</summary>
    public string CollectionName { get; set; } = "faces";

    /// <summary>Use the CUDA execution provider when available (requires the GPU ONNX Runtime package).</summary>
    public bool UseCuda { get; set; }
}
