using System.Data.Common;
using Amazon.Runtime;
using Amazon.S3;

namespace SaBai.API.Storage;

public static class RustFsS3
{
    public static RustFsSettings Read(IConfiguration configuration)
    {
        var settings =
            configuration.GetSection(RustFsSettings.SectionName).Get<RustFsSettings>()
            ?? new RustFsSettings();
        var connectionString =
            configuration.GetConnectionString("fs-sabai")
            ?? configuration.GetConnectionString("fs");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return settings;
        }

        var parsed = new DbConnectionStringBuilder { ConnectionString = connectionString };
        settings.Endpoint = Value(parsed, "Endpoint") ?? settings.Endpoint;
        settings.AccessKey = Value(parsed, "AccessKey") ?? settings.AccessKey;
        settings.SecretKey = Value(parsed, "SecretKey") ?? settings.SecretKey;
        settings.Bucket = Value(parsed, "Bucket") ?? settings.Bucket;
        return settings;
    }

    public static IAmazonS3 CreateClient(RustFsSettings settings)
    {
        if (
            string.IsNullOrWhiteSpace(settings.Endpoint)
            || string.IsNullOrWhiteSpace(settings.AccessKey)
            || string.IsNullOrWhiteSpace(settings.SecretKey)
        )
        {
            throw new InvalidOperationException(
                "RustFS connection string 'fs-sabai' is missing Endpoint, AccessKey, or SecretKey."
            );
        }

        var config = new AmazonS3Config
        {
            ServiceURL = settings.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = string.IsNullOrWhiteSpace(settings.Region)
                ? "us-east-1"
                : settings.Region,
            UseHttp = settings.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        return new AmazonS3Client(
            new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            config
        );
    }

    public static async Task EnsureBucketAsync(
        IAmazonS3 s3,
        RustFsSettings settings,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await s3.PutBucketAsync(settings.Bucket, cancellationToken);
                return;
            }
            catch (AmazonS3Exception exception)
                when (exception.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                return;
            }
            catch (Exception) when (attempt < 10)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private static string? Value(DbConnectionStringBuilder parsed, string key) =>
        parsed.TryGetValue(key, out var value) ? value?.ToString() : null;
}
