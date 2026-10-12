namespace SaBai.API.Auth;

public sealed class WorkOsSettings
{
    public const string SectionName = "WorkOS";

    public string ApiKey { get; set; } = "";

    public string ClientId { get; set; } = "";

    public bool UseFake { get; set; }
}

public sealed class AuthSettings
{
    public const string SectionName = "Auth";

    public string JwtSigningKey { get; set; } = "";

    public string Issuer { get; set; } = "sabai";

    public string Audience { get; set; } = "sabai-api";

    public int TokenLifetimeMinutes { get; set; } = 480;
}
