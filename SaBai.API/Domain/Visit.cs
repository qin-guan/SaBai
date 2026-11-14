namespace SaBai.API.Domain;

public sealed class Visit : ISyncEntity
{
    public const string StartedStatus = "started";

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public Guid VillageCodeId { get; set; }

    public VillageCode VillageCode { get; set; } = null!;

    public DateTimeOffset Date { get; set; }

    public string Status { get; set; } = StartedStatus;

    public Vitals? Vitals { get; set; }

    public Eyesight? Eyesight { get; set; }

    public DentalConsult? DentalConsult { get; set; }

    public ICollection<Consult> Consults { get; set; } = [];
}
