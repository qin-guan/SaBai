using System.Security.Claims;
using FastEndpoints;
using SaBai.API.Auth;
using SaBai.API.Sync;

namespace SaBai.API.Endpoints.Sync.Token.Get;

public sealed record Response(string Token, string Endpoint, DateTimeOffset ExpiresAt);

public sealed class Endpoint(SyncTokenService tokens, IConfiguration configuration)
    : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/sync/token");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s => s.Summary = "Mint a short-lived PowerSync JWT for the signed-in staff user");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();
        var (token, expiresAt) = tokens.CreateToken(userId, roles);
        var endpoint = configuration["Sync:PublicEndpoint"] ?? "/powersync";
        await Send.OkAsync(new Response(token, endpoint, expiresAt), ct);
    }
}
