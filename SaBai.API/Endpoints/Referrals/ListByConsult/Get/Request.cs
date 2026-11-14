namespace SaBai.API.Endpoints.Referrals.ListByConsult.Get;

/// <summary>Consult whose referrals should be listed.</summary>
public sealed record Request
{
    public Guid ConsultId { get; init; }
}
