namespace SaBai.API.Endpoints.Puberty.Create.Post;

/// <summary>Puberty assessment to record against vitals.</summary>
public sealed record Request
{
    public int VitalsId { get; init; }

    public bool? Pubarche { get; init; }

    public int? PubarcheAge { get; init; }

    public bool? Thelarche { get; init; }

    public int? ThelarcheAge { get; init; }

    public bool? Menarche { get; init; }

    public int? MenarcheAge { get; init; }

    public bool? VoiceChange { get; init; }

    public int? VoiceChangeAge { get; init; }

    public bool? TesticularGrowth { get; init; }

    public int? TesticularGrowthAge { get; init; }

    public string? AdditionalNotes { get; init; }
}
