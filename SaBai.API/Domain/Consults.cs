namespace SaBai.API.Domain;

public sealed class Consult
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromHours(24);

    public int Id { get; set; }

    public DateTimeOffset Date { get; set; }

    public string? PastMedicalHistory { get; set; }

    public string? Consultation { get; set; }

    public string? TreatmentPlan { get; set; }

    public string? ReferredFor { get; set; }

    public string? ReferralNotes { get; set; }

    public string? Remarks { get; set; }

    public Guid DoctorId { get; set; }

    public StaffUser Doctor { get; set; } = null!;

    public int VisitId { get; set; }

    public Visit Visit { get; set; } = null!;

    public ICollection<Diagnosis> Diagnoses { get; set; } = [];

    public ICollection<Referral> Referrals { get; set; } = [];

    public ICollection<Order> Orders { get; set; } = [];

    public bool IsEditLocked(DateTimeOffset now) => now - Date > EditWindow;
}

public sealed class Diagnosis
{
    public int Id { get; set; }

    public required string Details { get; set; }

    public DiagnosisCategory? Category { get; set; }

    public int ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;
}

public sealed class Referral
{
    public int Id { get; set; }

    public required string ReferredFor { get; set; }

    public string? ReferralNotes { get; set; }

    public ReferralState ReferralState { get; set; } = ReferralState.New;

    public string? ReferralOutcome { get; set; }

    public int ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
