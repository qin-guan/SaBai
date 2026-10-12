using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Puberty.Create.Post;

public sealed class Endpoint(AppDbContext db) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/puberty");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Record a puberty assessment");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
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

        _ = await db
            .Vitals.AsNoTracking()
            .FirstOrNotFoundAsync(x => x.Id == req.VitalsId, "Vitals were not found.", ct);

        if (await db.PubertyAssessments.AnyAsync(x => x.VitalsId == req.VitalsId, ct))
        {
            throw new ConflictException(
                "A puberty assessment already exists for this vitals record."
            );
        }

        var puberty = new PubertyAssessment
        {
            VitalsId = req.VitalsId,
            Pubarche = req.Pubarche,
            PubarcheAge = req.PubarcheAge,
            Thelarche = req.Thelarche,
            ThelarcheAge = req.ThelarcheAge,
            Menarche = req.Menarche,
            MenarcheAge = req.MenarcheAge,
            VoiceChange = req.VoiceChange,
            VoiceChangeAge = req.VoiceChangeAge,
            TesticularGrowth = req.TesticularGrowth,
            TesticularGrowthAge = req.TesticularGrowthAge,
            AdditionalNotes = FreeText.Normalize(req.AdditionalNotes),
        };
        db.PubertyAssessments.Add(puberty);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
            )
        {
            throw new ConflictException(
                "A puberty assessment already exists for this vitals record."
            );
        }

        await Send.CreatedAtAsync<Get.Endpoint>(
            new { puberty.Id },
            Response.From(puberty),
            cancellation: ct
        );
    }
}
