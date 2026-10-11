namespace SaBai.API.Endpoints.Auth.Users.List.Get;

/// <summary>Staff directory page.</summary>
public sealed record Response(IReadOnlyList<UserResponse> Users);
