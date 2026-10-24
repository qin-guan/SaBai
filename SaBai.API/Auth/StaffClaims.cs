using System.Security.Claims;
using SaBai.API.Domain;

namespace SaBai.API.Auth;

public static class StaffRoles
{
    public const string Admin = "admin";
    public const string Member = "member";
}

public static class StaffClaims
{
    public static Guid Id(this ClaimsPrincipal user) =>
        Guid.Parse(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new AuthRejectedException()
        );

    public static string Email(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? "";

    public static string Role(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? StaffRoles.Member;
}
