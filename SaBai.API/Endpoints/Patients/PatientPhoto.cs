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

    /// <summary>The object key for a photo; its extension follows the type found in its bytes.</summary>
    public static string BuildKey(Guid patientId, string contentType) =>
        $"patients/{patientId}/{Guid.NewGuid():N}{FileSignatures.ExtensionFor(contentType)}";

    /// <summary>
    /// Buffers the upload into memory and validates that it is a JPEG or PNG (by its bytes, not
    /// the client's Content-Type) that decodes, returning the raw bytes to store and their type.
    /// Records validation errors otherwise.
    /// </summary>
    public static async Task<(byte[] Bytes, string ContentType)?> ReadValidatedAsync(
        IEndpoint endpoint,
        IFormFile? file,
        string property,
        CancellationToken ct
    )
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
            var contentType = await FileSignatures.DetectAsync(probe, ct);
            if (contentType is null || !FileSignatures.IsImage(contentType))
            {
                endpoint.ValidationFailures.Add(new(property, "Only JPEG and PNG images are allowed."));
                return null;
            }

            probe.Position = 0;
            var image = RgbImage.Decode(probe);
            if ((long)image.Width * image.Height > MaxPixels)
            {
                endpoint.ValidationFailures.Add(new(property, "Image resolution is too large."));
                return null;
            }

            return (bytes, contentType);
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
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            await s3.DeleteObjectAsync(
                new DeleteObjectRequest { BucketName = storage.Bucket, Key = key },
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete patient photo object {Key}", key);
        }
    }
}
