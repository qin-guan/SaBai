namespace SaBai.API.Endpoints.Tags.List.Get;

/// <summary>Distinct freeform tag names ordered alphabetically.</summary>
public sealed record Response(IReadOnlyList<string> Tags);
