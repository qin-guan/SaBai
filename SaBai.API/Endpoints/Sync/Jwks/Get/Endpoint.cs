using FastEndpoints;
using SaBai.API.Sync;

namespace SaBai.API.Endpoints.Sync.Jwks.Get;

public sealed class Endpoint(SyncTokenService tokens) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/.well-known/jwks.json");
        AllowAnonymous();
        Summary(s => s.Summary = "Public JWKS for PowerSync token verification");
    }

    public override Task HandleAsync(CancellationToken ct) =>
        Send.StringAsync(tokens.GetJwksJson(), 200, "application/json", ct);
}
