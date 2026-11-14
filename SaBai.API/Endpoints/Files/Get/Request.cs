namespace SaBai.API.Endpoints.Files.Get;

/// <summary>Identifies the clinical file to read.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
