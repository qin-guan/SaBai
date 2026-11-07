namespace SaBai.API.Endpoints.Auth.Account.RecoveryCodes.Post;

/// <summary>One-time recovery codes. Shown once; earlier codes stop working.</summary>
public sealed record Response(IReadOnlyList<string> RecoveryCodes);
