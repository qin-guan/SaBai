namespace SaBai.API.Endpoints.Files.Content.Get;

/// <summary>Identifies the clinical file to download.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
