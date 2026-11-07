using SaBai.API.Endpoints.Auth;

namespace SaBai.API.Endpoints.Auth.WhoAmI.Get;

/// <summary>The staff user represented by the current session cookie.</summary>
public sealed record Response(UserResponse User);
