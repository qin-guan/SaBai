namespace SaBai.API.FaceRecognition;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the ONNX face recognition pipeline and the Qdrant-backed face store.
    /// Requires a <see cref="Qdrant.Client.QdrantClient"/> to be registered.</summary>
    public static IHostApplicationBuilder AddFaceRecognition(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<FaceRecognitionOptions>()
            .Bind(builder.Configuration.GetSection(FaceRecognitionOptions.SectionName))
            .Validate(o => o.DetectionSize > 0 && o.DetectionSize % 32 == 0, "DetectionSize must be a positive multiple of 32.")
            .Validate(o => o.MatchThreshold is >= -1 and <= 1, "MatchThreshold must be between -1 and 1.")
            .ValidateOnStart();

        builder.Services.AddSingleton<FaceModelProvisioner>();
        builder.Services.AddSingleton<FaceAnalyzer>();
        builder.Services.AddSingleton<FaceStore>();
        builder.Services.AddHostedService<FaceRecognitionInitializer>();
        return builder;
    }
}
