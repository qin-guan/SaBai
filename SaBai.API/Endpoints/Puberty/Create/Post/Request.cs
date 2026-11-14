namespace SaBai.API.Endpoints.Puberty.Create.Post;

/// <summary>Puberty assessment to record against vitals.</summary>
public sealed record Request
{
    /// <summary>Id for the new record, chosen by the client; generated when omitted. Resending a create with the same id returns the existing record.</summary>
    public Guid? Id { get; init; }

    public Guid VitalsId { get; init; }

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
