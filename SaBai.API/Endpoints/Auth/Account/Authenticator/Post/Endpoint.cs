using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SaBai.API.Auth;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth.Account.Authenticator.Post;

public sealed class Endpoint(
    UserManager<StaffUser> users,
    SignInManager<StaffUser> signIn,
    IOptions<AuthSettings> settings
) : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Post("/auth/account/authenticator");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
        {
            s.Summary = "Start authenticator (TOTP) setup";
            s.Description =
                "Generates a new authenticator secret. Two-factor stays off until the code "
                + "from the app is confirmed with `POST /auth/account/two-factor/enable`.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await users.GetCurrentAsync(User);
        if (await users.GetTwoFactorEnabledAsync(user))
        {
            throw new ConflictException(
                "Two-factor authentication is already on. Turn it off before setting up a new authenticator."
            );
        }

        (await users.ResetAuthenticatorKeyAsync(user)).ThrowIfFailed();
        // Resetting the key rotates the security stamp; re-issue this session's cookie.
        await signIn.RefreshSignInAsync(user);

        var key = await users.GetAuthenticatorKeyAsync(user) ?? "";
        var issuer = Uri.EscapeDataString(settings.Value.AuthenticatorIssuer);
        var account = Uri.EscapeDataString(user.UserName ?? user.Id.ToString());
        await Send.OkAsync(
            new Response(
                Format(key),
                $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6"
            ),
            ct
        );
    }

    private static string Format(string key)
    {
        var result = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (i > 0)
            {
                result.Append(' ');
            }

            result.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }

        return result.ToString().ToLowerInvariant();
    }
}
