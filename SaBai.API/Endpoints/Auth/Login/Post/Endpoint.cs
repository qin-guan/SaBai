using System.Security.Claims;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Login.Post;

public sealed class Endpoint(AppDbContext db, IWorkOsDirectory directory, IOptions<AuthSettings> auth)
    : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Sign in with email and password";
            s.Description = "Authenticates with WorkOS and returns a bearer token for this API.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var identity = await directory.AuthenticateAsync(req.Email, req.Password, ct);
        var user = await db.StaffUsers.FirstOrDefaultAsync(x => x.WorkOsUserId == identity.Id || x.Email == identity.Email, ct)
            ?? throw new AuthRejectedException();

        if (user.IsLocked)
        {
            throw new ForbiddenException("Account is locked.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(auth.Value.TokenLifetimeMinutes);
        var token = JwtBearer.CreateToken(o =>
        {
            o.SigningKey = auth.Value.JwtSigningKey;
            o.Issuer = auth.Value.Issuer;
            o.Audience = auth.Value.Audience;
            o.ExpireAt = expiresAt.UtcDateTime;
            o.User.Roles.Add(user.Role == StaffRole.Admin ? StaffRoles.Admin : StaffRoles.Member);
            o.User.Claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            o.User.Claims.Add(new Claim(ClaimTypes.Email, user.Email));
            o.User.Claims.Add(new Claim("username", user.Username));
            o.User.Claims.Add(new Claim("nickname", user.Nickname));
        });

        await Send.OkAsync(new Response(token, expiresAt, UserResponse.From(user)), ct);
    }
}
