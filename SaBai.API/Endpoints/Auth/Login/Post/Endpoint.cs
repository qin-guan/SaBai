using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Login.Post;

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
        Post("/auth/login");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Sign in with username and password";
            s.Description =
                "Checks the password with ASP.NET Core Identity and sets the session cookie. "
                + "Repeated failures lock the account for a few minutes. When the account has "
                + "two-factor on, no session is created yet: `requiresTwoFactor` is true and the "
                + "client continues with `POST /auth/login/two-factor`.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var name = req.UserName.Trim();
        var user = await users.FindByNameAsync(name);
        if (user is null && name.Contains('@'))
        {
            user = await users.FindByEmailAsync(name);
        }

        if (user is null)
        {
            await audit.RecordAsync(
                AuditAction.SignInFailed,
                userName: name,
                details: "Password: unknown user",
                ct: ct
            );
            throw new AuthRejectedException();
        }

        var result = await signIn.PasswordSignInAsync(
            user,
            req.Password,
            req.RememberMe,
            lockoutOnFailure: true
        );
        await Send.OkAsync(await SignInResponse.FromAsync(result, user, users, db, audit, "Password", ct), ct);
    }
}
