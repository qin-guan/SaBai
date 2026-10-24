using SaBai.API.Endpoints.Auth;

namespace SaBai.API.Endpoints.Auth.WhoAmI.Get;

/// <summary>The staff user represented by the current bearer token.</summary>
public sealed record Response(UserResponse User);
