using FastEndpoints;

namespace SaBai.API.Endpoints.Patients.SearchByFace.Post;

/// <summary>Multipart search for a patient by face photo.</summary>
public sealed class Request
{
    public IFormFile Image { get; set; } = null!;

    /// <summary>Minimum cosine similarity for a match. Defaults to the configured match threshold.</summary>
    [QueryParam]
    public float? Threshold { get; set; }

    /// <summary>Maximum number of candidate patients returned.</summary>
    [QueryParam]
    public int MaxMatches { get; set; } = 5;
}
