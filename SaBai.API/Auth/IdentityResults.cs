using Microsoft.AspNetCore.Identity;
using SaBai.API.Domain;

namespace SaBai.API.Auth;

public static class IdentityResults
{
    /// <summary>
    /// Turns a failed <see cref="IdentityResult"/> into the API's problem responses:
    /// duplicates are 409, everything else (password rules, bad usernames…) is 400.
    /// Never 401, so a failed self-service call does not log the web app out.
    /// </summary>
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var message = string.Join(" ", result.Errors.Select(x => x.Description));
        if (result.Errors.Any(x => x.Code is "DuplicateUserName" or "DuplicateEmail"))
        {
            throw new ConflictException(message);
        }

        throw new BadRequestException(message);
    }

    public static async Task<StaffUser> FindByIdOrNotFoundAsync(
        this UserManager<StaffUser> users,
        Guid id
    ) =>
        await users.FindByIdAsync(id.ToString())
        ?? throw new NotFoundException("Staff user was not found.");

    /// <summary>The signed-in user, or 401 if the cookie points at a user that is gone.</summary>
    public static async Task<StaffUser> GetCurrentAsync(
        this UserManager<StaffUser> users,
        System.Security.Claims.ClaimsPrincipal principal
    ) => await users.GetUserAsync(principal) ?? throw new AuthRejectedException();
}
