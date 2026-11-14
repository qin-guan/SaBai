using FastEndpoints;

namespace SaBai.API.Endpoints.Fhir.Patients.Search;

public sealed class Request
{
    [QueryParam]
    public string? Name { get; set; }

    [QueryParam]
    public string? Identifier { get; set; }

    [QueryParam, BindFrom("_count")]
    public int? Count { get; set; }
}
