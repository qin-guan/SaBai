namespace SaBai.API.Domain;

public sealed class Broadcast
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    public BroadcastSeverity Severity { get; set; } = BroadcastSeverity.Info;

    public string? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int Attempts { get; set; }
}
