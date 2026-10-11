using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using SaBai.API.FaceRecognition;
using SaBai.API.Storage;

namespace SaBai.API.Endpoints.Patients;

/// <summary>Helpers for storing a single profile photo per patient in the RustFS bucket.</summary>
public static class PatientPhoto
{
    /// <summary>Maximum accepted photo size.</summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    private const long MaxPixels = 50_000_000;

    public static string BuildKey(int patientId, string? fileName)
        => $"patients/{patientId}/{Guid.NewGuid():N}{Extension(fileName)}";

    public static string Extension(string? fileName)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName ?? ""));
        var cleaned = new string(extension.TrimStart('.').Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrEmpty(cleaned) ? ".jpg" : $".{cleaned.ToLowerInvariant()}";
    }

    public static string ContentType(string? contentType, string extension)
        => !string.IsNullOrWhiteSpace(contentType)
            ? contentType
            : extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "image/jpeg",
            };

    /// <summary>
    /// Buffers the upload into memory and validates that it decodes as an image,
    /// returning the raw bytes to store. Records validation errors otherwise.
    /// </summary>
    public static async Task<byte[]?> ReadValidatedAsync(
        IEndpoint endpoint,
        IFormFile? file,
        string property,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            endpoint.ValidationFailures.Add(new(property, "An image file is required."));
            return null;
        }

        if (file.Length > MaxBytes)
        {
            endpoint.ValidationFailures.Add(new(property, "Image is too large."));
            return null;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();

            using var probe = new MemoryStream(bytes);
            var image = RgbImage.Decode(probe);
            if ((long)image.Width * image.Height > MaxPixels)
            {
                endpoint.ValidationFailures.Add(new(property, "Image resolution is too large."));
                return null;
            }

            return bytes;
        }
        catch (InvalidImageException ex)
        {
            endpoint.ValidationFailures.Add(new(property, ex.Message));
            return null;
        }
    }

    /// <summary>Best-effort removal of a stored object; failures are logged, never thrown.</summary>
    public static async Task DeleteObjectAsync(
        IAmazonS3 s3,
        RustFsSettings storage,
        string? key,
        ILogger logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            await s3.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = storage.Bucket, Key = key },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete patient photo object {Key}", key);
        }
    }
}
