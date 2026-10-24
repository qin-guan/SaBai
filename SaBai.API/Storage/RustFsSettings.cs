namespace SaBai.API.Storage;

/// <summary>S3-compatible settings for the RustFS bucket referenced by the AppHost.</summary>
public sealed class RustFsSettings
{
    public const string SectionName = "Storage";

    public string Bucket { get; set; } = "sabai";

    public string Region { get; set; } = "us-east-1";

    public string Endpoint { get; set; } = "";

    public string AccessKey { get; set; } = "";

    public string SecretKey { get; set; } = "";
}
