namespace SaBai.API.Endpoints.Auth.Roles.List.Get;

/// <summary>Role names that can be given to staff users.</summary>
public sealed record Response(IReadOnlyList<string> Roles);
