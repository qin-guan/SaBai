using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.LoginRecoveryCode.Post;

public sealed class Endpoint(
    UserManager<StaffUser> users,
    SignInManager<StaffUser> signIn,
    AppDbContext db,
    AuditTrail audit
)
    : Endpoint<Request, SignInResponse>
{
    public override void Configure()
    {
        Post("/auth/login/recovery-code");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Finish sign-in with a one-time recovery code";
            s.Description =
                "Must follow a `POST /auth/login` that returned `requiresTwoFactor`. "
                + "Each recovery code works once.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user =
            await signIn.GetTwoFactorAuthenticationUserAsync() ?? throw new AuthRejectedException();
        var result = await signIn.TwoFactorRecoveryCodeSignInAsync(
            req.RecoveryCode.Replace(" ", "")
        );
        await Send.OkAsync(await SignInResponse.FromAsync(result, user, users, db, audit, "Recovery code", ct), ct);
    }
}
