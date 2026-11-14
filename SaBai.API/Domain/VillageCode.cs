namespace SaBai.API.Domain;

public sealed class VillageCode : ISyncEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string ColorHex { get; set; }

    public bool IsVisible { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Patient> Patients { get; set; } = [];

    public ICollection<Visit> Visits { get; set; } = [];
}
