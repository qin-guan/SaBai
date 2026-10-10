using FastEndpoints;

namespace SaBai.API.Endpoints.Auth.WhoAmI.Get;

public class Endpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/whoami");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new {}, ct);
    }
}