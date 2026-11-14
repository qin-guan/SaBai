namespace SaBai.API.Endpoints.Consults.Pdf.Get;

/// <summary>Identifies the consult to render as a PDF.</summary>
public sealed record Request
{
    public Guid Id { get; init; }
}
