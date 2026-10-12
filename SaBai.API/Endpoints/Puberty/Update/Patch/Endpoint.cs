using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Data;

namespace SaBai.API.Endpoints.Puberty.Update.Patch;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Patch("/puberty/{Id}");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Update a puberty assessment");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = await db.PubertyAssessments.FirstOrNotFoundAsync(
            x => x.Id == req.Id,
            "Puberty assessment was not found.",
            ct
        );

        PubertyRules.EnsureAges(
            req.Pubarche,
            req.PubarcheAge,
            req.Thelarche,
            req.ThelarcheAge,
            req.Menarche,
            req.MenarcheAge,
            req.VoiceChange,
            req.VoiceChangeAge,
            req.TesticularGrowth,
            req.TesticularGrowthAge
        );

        if (req.Pubarche is { } pubarche)
        {
            entity.Pubarche = pubarche;
        }

        if (req.PubarcheAge is { } pubarcheAge)
        {
            entity.PubarcheAge = pubarcheAge;
        }

        if (req.Thelarche is { } thelarche)
        {
            entity.Thelarche = thelarche;
        }

        if (req.ThelarcheAge is { } thelarcheAge)
        {
            entity.ThelarcheAge = thelarcheAge;
        }

        if (req.Menarche is { } menarche)
        {
            entity.Menarche = menarche;
        }

        if (req.MenarcheAge is { } menarcheAge)
        {
            entity.MenarcheAge = menarcheAge;
        }

        if (req.VoiceChange is { } voiceChange)
        {
            entity.VoiceChange = voiceChange;
        }

        if (req.VoiceChangeAge is { } voiceChangeAge)
        {
            entity.VoiceChangeAge = voiceChangeAge;
        }

        if (req.TesticularGrowth is { } testicularGrowth)
        {
            entity.TesticularGrowth = testicularGrowth;
        }

        if (req.TesticularGrowthAge is { } testicularGrowthAge)
        {
            entity.TesticularGrowthAge = testicularGrowthAge;
        }

        if (req.AdditionalNotes is not null)
        {
            entity.AdditionalNotes = FreeText.Normalize(req.AdditionalNotes);
        }

        await db.SaveChangesAsync(ct);
        await Send.OkAsync(Response.From(entity), ct);
    }
}
