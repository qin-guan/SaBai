using SaBai.API.Endpoints.Auth;

namespace SaBai.API.Endpoints.Auth.Login.Post;

/// <summary>Bearer token issued after a successful WorkOS password login.</summary>
public sealed record Response(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
