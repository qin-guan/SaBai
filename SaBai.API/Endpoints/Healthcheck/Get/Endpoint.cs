using FastEndpoints;

namespace SaBai.API.Endpoints.Healthcheck.Get;

public sealed class Endpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/healthcheck");
        AllowAnonymous();
        Summary(s => s.Summary = "Public liveness string");
    }

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync("yay!", ct);
}
