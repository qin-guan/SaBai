using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Puberty.GetByVitals.Get;

/// <summary>Puberty assessment recorded against vitals.</summary>
public sealed record Response(
    int Id,
    int VitalsId,
    bool? Pubarche,
    int? PubarcheAge,
    bool? Thelarche,
    int? ThelarcheAge,
    bool? Menarche,
    int? MenarcheAge,
    bool? VoiceChange,
    int? VoiceChangeAge,
    bool? TesticularGrowth,
    int? TesticularGrowthAge,
    string? AdditionalNotes)
{
    public static Response From(PubertyAssessment puberty) => new(
        puberty.Id,
        puberty.VitalsId,
        puberty.Pubarche,
        puberty.PubarcheAge,
        puberty.Thelarche,
        puberty.ThelarcheAge,
        puberty.Menarche,
        puberty.MenarcheAge,
        puberty.VoiceChange,
        puberty.VoiceChangeAge,
        puberty.TesticularGrowth,
        puberty.TesticularGrowthAge,
        puberty.AdditionalNotes);
}
