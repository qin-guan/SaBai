namespace SaBai.API.Endpoints.Broadcasts.List.Get;

/// <summary>
/// Broadcast list paging. Limit defaults to 50 (max 200). Since returns every broadcast with an
/// id above it and ignores Limit, so a client that moves Since forward never skips older new rows.
/// </summary>
public sealed record Request
{
    public const int DefaultLimit = 50;

    public const int MaxLimit = 200;

    public int? Limit { get; init; }

    public Guid? Since { get; init; }
}
