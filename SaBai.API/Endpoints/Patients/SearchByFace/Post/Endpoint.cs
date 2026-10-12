using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;
using SaBai.API.Endpoints.Faces;
using SaBai.API.FaceRecognition;

namespace SaBai.API.Endpoints.Patients.SearchByFace.Post;

public sealed class Endpoint(
    AppDbContext db,
    FaceAnalyzer analyzer,
    FaceStore store,
    IOptions<FaceRecognitionOptions> options
) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/patients/search-by-face");
        AllowFileUploads();
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Search for patients by face photo");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.Threshold is < -1 or > 1)
        {
            AddError(r => r.Threshold!, "Threshold must be between -1 and 1.");
        }

        if (req.MaxMatches is < 1 or > 20)
        {
            AddError(r => r.MaxMatches, "MaxMatches must be between 1 and 20.");
        }

        var image = await FaceImages.LoadAsync(this, req.Image, nameof(req.Image), ct);
        ThrowIfAnyErrors();

        var threshold = req.Threshold ?? options.Value.MatchThreshold;
        var faces = await analyzer.AnalyzeAsync(image!, ct);

        // Best similarity per patient across every detected face.
        var best = new Dictionary<int, float>();
        foreach (var face in faces)
        {
            var matches = await store.SearchAsync(face.Embedding, threshold, req.MaxMatches, ct);
            foreach (var match in matches)
            {
                if (
                    PatientFaceIdentity.TryParse(match.PersonId, out var patientId)
                    && (!best.TryGetValue(patientId, out var current) || match.Similarity > current)
                )
                {
                    best[patientId] = match.Similarity;
                }
            }
        }

        if (best.Count == 0)
        {
            await Send.OkAsync(new Response([]), ct);
            return;
        }

        var ids = best.Keys.ToArray();
        var rows = await db
            .Patients.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.PhotoStoredFileName,
                VillageCode = x.VillageCode != null ? x.VillageCode.Code : null,
                VillageColorHex = x.VillageCode != null ? x.VillageCode.ColorHex : null,
            })
            .ToListAsync(ct);

        var results = rows.Select(x => new PatientFaceMatch(
                x.Id,
                x.Name,
                Patient.FormatDisplayId(x.VillageCode, x.Id),
                x.VillageCode,
                x.VillageColorHex,
                x.PhotoStoredFileName != null,
                best[x.Id]
            ))
            .OrderByDescending(m => m.Similarity)
            .Take(req.MaxMatches)
            .ToList();

        await Send.OkAsync(new Response(results), ct);
    }
}
