namespace SaBai.API.Endpoints.Eyesight.Get;

/// <summary>Identifies an eyesight record.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
