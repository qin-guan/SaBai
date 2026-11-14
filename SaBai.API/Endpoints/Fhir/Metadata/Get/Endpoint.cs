using FastEndpoints;
using SaBai.API.Fhir;

namespace SaBai.API.Endpoints.Fhir.Metadata.Get;

public sealed class Endpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/fhir/r4/metadata");
        AllowAnonymous();
        Summary(s => s.Summary = "HL7 FHIR R4 CapabilityStatement");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/fhir/r4";
        var statement = FhirMapper.BuildCapabilityStatement(baseUrl);
        await Send.StringAsync(
            FhirMapper.Serialize(statement),
            StatusCodes.Status200OK,
            FhirMapper.FhirJsonContentType,
            ct
        );
    }
}
