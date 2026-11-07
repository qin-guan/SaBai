using Microsoft.AspNetCore.Identity;
using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Auth;

/// <summary>
/// Result of a sign-in step. On success the session cookie is set and <c>User</c> is
/// returned. When the account has two-factor on, <c>RequiresTwoFactor</c> is true and
/// the client must finish with <c>POST /auth/login/two-factor</c> (or a recovery code).
/// </summary>
public sealed record SignInResponse(bool RequiresTwoFactor, UserResponse? User)
{
    public static async Task<SignInResponse> FromAsync(
        SignInResult result,
        StaffUser user,
        UserManager<StaffUser> users
    )
    {
        if (result.Succeeded)
        {
            return new SignInResponse(false, await UserResponse.FromAsync(user, users));
        }

        if (result.RequiresTwoFactor)
        {
            return new SignInResponse(true, null);
        }

        if (result.IsLockedOut)
        {
            throw new ForbiddenException(
                "This account is locked. Try again later or ask an admin to unlock it."
            );
        }

        throw new AuthRejectedException();
    }
}
