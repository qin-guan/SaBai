namespace SaBai.API.Endpoints.Broadcasts.List.Get;

/// <summary>Broadcast list paging. Limit defaults to 50 (max 200); Since returns only ids above it.</summary>
public sealed record Request
{
    public const int DefaultLimit = 50;

    public const int MaxLimit = 200;

    public int? Limit { get; init; }

    public int? Since { get; init; }
}
