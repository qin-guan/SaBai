using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.LoginTwoFactor.Post;

public sealed class Endpoint(
    UserManager<StaffUser> users,
    SignInManager<StaffUser> signIn,
    AuditTrail audit
)
    : Endpoint<Request, SignInResponse>
{
    public override void Configure()
    {
        Post("/auth/login/two-factor");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Finish sign-in with an authenticator code";
            s.Description =
                "Must follow a `POST /auth/login` that returned `requiresTwoFactor`. "
                + "Sets the session cookie on success.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user =
            await signIn.GetTwoFactorAuthenticationUserAsync() ?? throw new AuthRejectedException();
        var code = req.Code.Replace(" ", "").Replace("-", "");
        var result = await signIn.TwoFactorAuthenticatorSignInAsync(
            code,
            req.RememberMe,
            req.RememberMachine
        );
        await Send.OkAsync(await SignInResponse.FromAsync(result, user, users, audit, "Authenticator code", ct), ct);
    }
}
