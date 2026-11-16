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
    public const string Doctor = "doctor";
    public const string Nurse = "nurse";
    public const string Pharmacist = "pharmacist";
    public const string Dentist = "dentist";
    public const string Optometrist = "optometrist";
    public const string Registration = "registration";
    public const string Auditor = "auditor";

    public static readonly IReadOnlyList<string> All =
    [
        Admin,
        Member,
        Pn,
        Doctor,
        Nurse,
        Pharmacist,
        Dentist,
        Optometrist,
        Registration,
        Auditor,
    ];

    public static readonly string[] Authenticated = [.. All];
    public static readonly string[] Clinic = [Admin, Member, Doctor, Nurse, Pharmacist, Dentist, Optometrist, Registration];
    public static readonly string[] PatientOverview = [Admin, Member, Pn, Doctor, Nurse, Pharmacist, Dentist, Optometrist, Registration];
    public static readonly string[] RegistrationWork = [Admin, Member, Registration];
    public static readonly string[] DoctorWork = [Admin, Member, Doctor];
    public static readonly string[] NurseWork = [Admin, Member, Nurse];
    public static readonly string[] PharmacyWork = [Admin, Member, Pharmacist];
    public static readonly string[] OrderWork = [Admin, Member, Doctor, Pharmacist];
    public static readonly string[] DentalWork = [Admin, Member, Dentist];
    public static readonly string[] OptometryWork = [Admin, Member, Optometrist];
    public static readonly string[] ReferralWork = [Admin, Member, Pn, Doctor];
    public static readonly string[] PatientNavigation = [Admin, Member, Pn];
    public static readonly string[] ConsentWork = [Admin, Member, Doctor, Nurse, Registration];
    public static readonly string[] DisclosureRead = [Admin, Member, Doctor, Auditor];
    public static readonly string[] DisclosureWrite = [Admin, Member, Doctor];
    public static readonly string[] FhirSearch = [Admin, Member, Doctor, Nurse, Registration];

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
