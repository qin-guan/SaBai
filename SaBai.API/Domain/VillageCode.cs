namespace SaBai.API.Domain;

public sealed class VillageCode
{
    public int Id { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public required string ColorHex { get; set; }

    public bool IsVisible { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Patient> Patients { get; set; } = [];

    public ICollection<Visit> Visits { get; set; } = [];
}
