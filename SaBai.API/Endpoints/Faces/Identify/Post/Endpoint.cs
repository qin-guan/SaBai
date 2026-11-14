using FastEndpoints;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Endpoints.Patients;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Faces.Identify.Post;

public sealed class Request
{
    public IFormFile Image { get; set; } = null!;

    /// <summary>Minimum cosine similarity for a match. Defaults to the configured match threshold.</summary>
    [QueryParam]
    public float? Threshold { get; set; }

    /// <summary>Maximum number of candidate people returned per face.</summary>
    [QueryParam]
    public int MaxMatches { get; set; } = 1;
}

public sealed record IdentifiedFace(FaceDto Face, MatchDto[] Matches);

public sealed record Response(IdentifiedFace[] Faces);

public class Endpoint(
    AppDbContext db,
    FaceAnalyzer analyzer,
    FaceStore store,
    IOptions<FaceRecognitionOptions> options
) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/faces/identify");
        AllowFileUploads();
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Identify every face in an image against the enrolled people.");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // A caller may ask for stricter matching than configured, never looser.
        var configured = options.Value.MatchThreshold;
        if (req.Threshold is { } requested && !(requested >= configured && requested <= 1))
            AddError(r => r.Threshold!, $"Threshold must be between {configured} and 1.");
        if (req.MaxMatches is < 1 or > 20)
            AddError(r => r.MaxMatches, "MaxMatches must be between 1 and 20.");
        var image = await FaceImages.LoadAsync(this, req.Image, nameof(req.Image), ct);
        ThrowIfAnyErrors();

        var threshold = req.Threshold ?? configured;
        var faces = await analyzer.AnalyzeAsync(image!, ct);
        var matches = new IReadOnlyList<FaceMatch>[faces.Count];
        for (var i = 0; i < faces.Count; i++)
            matches[i] = await store.SearchAsync(faces[i].Embedding, threshold, req.MaxMatches, ct);

        // Patient faces only match while the patient's biometric consent is current.
        var patientIds = matches
            .SelectMany(m => m)
            .Select(m => PatientFaceIdentity.TryParse(m.PersonId, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .ToHashSet();
        var consenting = await BiometricConsent.GrantedAsync(db, patientIds, ct);

        var results = new IdentifiedFace[faces.Count];
        for (var i = 0; i < faces.Count; i++)
            results[i] = new IdentifiedFace(
                FaceDto.From(faces[i].Detection),
                matches[i]
                    .Where(m =>
                        !PatientFaceIdentity.TryParse(m.PersonId, out var id)
                        || consenting.Contains(id)
                    )
                    .Select(m => new MatchDto(m.PersonId, m.FaceId, m.Similarity))
                    .ToArray()
            );

        await Send.OkAsync(new Response(results), ct);
    }
}
