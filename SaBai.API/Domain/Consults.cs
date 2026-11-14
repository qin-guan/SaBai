namespace SaBai.API.Domain;

public sealed class Consult : ITrackedEntity
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromHours(24);

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public DateTimeOffset Date { get; set; }

    [PhiData]
    public string? PastMedicalHistory { get; set; }

    [PhiData]
    public string? Consultation { get; set; }

    [PhiData]
    public string? TreatmentPlan { get; set; }

    public string? ReferredFor { get; set; }

    [PhiData]
    public string? ReferralNotes { get; set; }

    [PhiData]
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

public sealed class Diagnosis : ITrackedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    [PhiData]
    public required string Details { get; set; }

    public DiagnosisCategory? Category { get; set; }

    /// <summary>Optional standard terminology URI (e.g. http://hl7.org/fhir/sid/icd-10 or http://snomed.info/sct).</summary>
    public string? CodingSystem { get; set; }

    /// <summary>Optional standard code within <see cref="CodingSystem"/>.</summary>
    public string? Code { get; set; }

    /// <summary>Optional preferred term for <see cref="Code"/>.</summary>
    public string? CodeDisplay { get; set; }

    public Guid ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;
}

public sealed class ReferralPartner : ITrackedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Remarks { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public ICollection<Referral> Referrals { get; set; } = [];
}

public sealed class Referral : ITrackedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public required string ReferredFor { get; set; }

    [PhiData]
    public string? ReferralNotes { get; set; }

    public ReferralState ReferralState { get; set; } = ReferralState.New;

    [PhiData]
    public string? ReferralOutcome { get; set; }

    public Guid ConsultId { get; set; }

    public Consult Consult { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    // Referral booth section
    public Guid? ReferredToId { get; set; }

    public ReferralPartner? ReferredTo { get; set; }

    public Guid? MemberInChargeId { get; set; }

    public StaffUser? MemberInCharge { get; set; }

    public GuardiansContacted? GuardiansContacted { get; set; }

    public ReferralServiceClass? ClassOfService { get; set; }

    public string? DescriptionOfService { get; set; }

    public string? PatientSummaryFromScreening { get; set; }

    public string? RemarksAtBooth { get; set; }

    // Referral party section
    public Guid? MemberOnRunId { get; set; }

    public StaffUser? MemberOnRun { get; set; }

    public bool PresentAtReferralParty { get; set; }

    public string? ReferralPartyResults { get; set; }

    public bool FollowUp { get; set; }

    public bool DoctorsUpdated { get; set; }

    // Money section
    public decimal? AmountPaidUsd { get; set; }

    public int? AmountPaidRiels { get; set; }

    public Guid? MemberWhoPaidId { get; set; }

    public StaffUser? MemberWhoPaid { get; set; }

    public bool IsReimbursed { get; set; }

    public DateTimeOffset? ReimbursedAt { get; set; }

    public Guid? ReimbursedByUserId { get; set; }

    public StaffUser? ReimbursedByUser { get; set; }

    public string? ReimbursementNotes { get; set; }
}
