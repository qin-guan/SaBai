using System.Security.Claims;
using SaBai.API.Domain;

namespace SaBai.API.Auth;

/// <summary>
/// Identity role names. Roles live in the Identity role store; this list is the set
/// the API knows how to authorize and is seeded at startup.
/// </summary>
public static class StaffRoles
{
    public const string Admin = "admin";
    public const string Member = "member";
    public const string Pn = "pn";

    public static readonly IReadOnlyList<string> All = [Admin, Member, Pn];

    public static bool IsKnown(string role) => All.Contains(role, StringComparer.Ordinal);
}

public static class StaffClaims
{
    public static Guid Id(this ClaimsPrincipal user) =>
        Guid.Parse(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new AuthRejectedException()
        );

    public static string UserName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? "";

    /// <summary>Email when the account has one, otherwise the username.</summary>
    public static string Email(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) is { Length: > 0 } email ? email : user.UserName();
}
