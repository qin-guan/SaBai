namespace SaBai.API.Domain;

public sealed class Consult : ISyncEntity
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromHours(24);

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset Date { get; set; }

    public string? PastMedicalHistory { get; set; }

    public string? Consultation { get; set; }

    public string? TreatmentPlan { get; set; }

    public string? ReferredFor { get; set; }

    public string? ReferralNotes { get; set; }

    public string? Remarks { get; set; }

    public Guid DoctorId { get; set; }

    public StaffUser Doctor { get; set; } = null!;

    public Guid VisitId { get; set; }

    public Visit Visit { get; set; } = null!;

    public ICollection<Diagnosis> Diagnoses { get; set; } = [];

    public ICollection<Referral> Referrals { get; set; } = [];

    public ICollection<Order> Orders { get; set; } = [];

    public bool IsEditLocked(DateTimeOffset now) => now - Date > EditWindow;
}

public sealed class Diagnosis : ISyncEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public required string Details { get; set; }

    public DiagnosisCategory? Category { get; set; }

    public Guid ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;
}

public sealed class Referral : ISyncEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public required string ReferredFor { get; set; }

    public string? ReferralNotes { get; set; }

    public ReferralState ReferralState { get; set; } = ReferralState.New;

    public string? ReferralOutcome { get; set; }

    public Guid ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
