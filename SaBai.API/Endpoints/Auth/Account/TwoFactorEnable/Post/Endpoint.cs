using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using SaBai.API.Auth;
using SaBai.API.Domain;
using RecoveryCodesResponse = SaBai.API.Endpoints.Auth.Account.RecoveryCodes.Post.Response;

namespace SaBai.API.Endpoints.Auth.Account.TwoFactorEnable.Post;

public sealed class Endpoint(UserManager<StaffUser> users, SignInManager<StaffUser> signIn)
    : Endpoint<Request, RecoveryCodesResponse>
{
    public override void Configure()
    {
        Post("/auth/account/two-factor/enable");
        Roles(StaffRoles.Admin, StaffRoles.Member);
        Summary(s =>
        {
            s.Summary = "Turn on two-factor authentication";
            s.Description =
                "Confirms the authenticator from `POST /auth/account/authenticator` and returns "
                + "ten one-time recovery codes.";
        });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var user = await users.GetCurrentAsync(User);
        if (await users.GetTwoFactorEnabledAsync(user))
        {
            throw new ConflictException("Two-factor authentication is already on.");
        }

        if (string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(user)))
        {
            throw new BadRequestException("Set up an authenticator first.");
        }

        var code = req.Code.Replace(" ", "").Replace("-", "");
        var valid = await users.VerifyTwoFactorTokenAsync(
            user,
            users.Options.Tokens.AuthenticatorTokenProvider,
            code
        );
        if (!valid)
        {
            throw new BadRequestException("The verification code is not valid.");
        }

        (await users.SetTwoFactorEnabledAsync(user, true)).ThrowIfFailed();
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await signIn.RefreshSignInAsync(user);
        await Send.OkAsync(new RecoveryCodesResponse(codes?.ToList() ?? []), ct);
    }
}
