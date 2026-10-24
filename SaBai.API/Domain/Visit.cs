namespace SaBai.API.Domain;

public sealed class Visit
{
    public const string StartedStatus = "started";

    public int Id { get; set; }

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public int VillageCodeId { get; set; }

    public VillageCode VillageCode { get; set; } = null!;

    public DateTimeOffset Date { get; set; }

    public string Status { get; set; } = StartedStatus;

    public Vitals? Vitals { get; set; }

    public Eyesight? Eyesight { get; set; }

    public ICollection<Consult> Consults { get; set; } = [];
}
