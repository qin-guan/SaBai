namespace SaBai.API.Auth;

public sealed class AuthSettings
{
    public const string SectionName = "Auth";

    /// <summary>How long a sign-in cookie stays valid without activity (sliding).</summary>
    public int SessionLifetimeMinutes { get; set; } = 480;

    /// <summary>
    /// How often a signed-in cookie is re-checked against the database, so role
    /// changes, locks and password resets reach existing sessions.
    /// </summary>
    public int SecurityStampValidationMinutes { get; set; } = 1;

    /// <summary>Issuer shown in authenticator apps next to the account name.</summary>
    public string AuthenticatorIssuer { get; set; } = "Sa'Bai";

    /// <summary>Seed the demo staff accounts (development and tests only).</summary>
    public bool SeedDemoUsers { get; set; }

    /// <summary>
    /// Optional first admin. When set, the account is created at startup if it does not
    /// exist, or given this password if it has none yet (for example right after the
    /// WorkOS migration, which carried no passwords over).
    /// </summary>
    public BootstrapAdminSettings BootstrapAdmin { get; set; } = new();
}

public sealed class BootstrapAdminSettings
{
    public string UserName { get; set; } = "";

    public string Password { get; set; } = "";

    public string? Email { get; set; }
}
