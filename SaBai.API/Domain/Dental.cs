namespace SaBai.API.Domain;

/// <summary>
/// The dental station's consultation for a visit: history, chief complaint, clinical
/// examination, diagnoses, treatment provided, referral and remarks. One per visit.
/// </summary>
public sealed class DentalConsult
{
    public int Id { get; set; }

    public int VisitId { get; set; }

    public Visit Visit { get; set; } = null!;

    // 1. Dental history

    public YesNoUnknown? PreviousDentalVisit { get; set; }

    public YesNoUnknown? PreviousDentalTreatment { get; set; }

    public bool? Toothache { get; set; }

    public bool? Extraction { get; set; }

    public bool? DentalTrauma { get; set; }

    public bool? CurrentToothache { get; set; }

    public bool? ToothSensitivity { get; set; }

    public bool? BleedingGums { get; set; }

    public BrushingFrequency? BrushingFrequency { get; set; }

    public YesNoUnknown? FluorideToothpaste { get; set; }

    public SugarConsumption? SugarConsumption { get; set; }

    public string? OtherDentalHistory { get; set; }

    // 2. Chief complaint

    public string? ChiefComplaint { get; set; }

    // 3A. Extraoral examination

    public FacialSymmetry? FacialSymmetry { get; set; }

    public AbsentPresent? FacialSwelling { get; set; }

    public Lymphadenopathy? Lymphadenopathy { get; set; }

    public MouthOpening? MouthOpening { get; set; }

    // 3B. Intraoral examination

    public OralHygiene? OralHygiene { get; set; }

    public PlaqueLevel? Plaque { get; set; }

    public AbsentPresent? Calculus { get; set; }

    public GingivalCondition? GingivalCondition { get; set; }

    public CariesStatus? Caries { get; set; }

    public AbsentPresent? MissingTeeth { get; set; }

    public AbsentPresent? FracturedTeeth { get; set; }

    public AbsentPresent? MucosalLesions { get; set; }

    public Malocclusion? Malocclusion { get; set; }

    // 4. Diagnosis

    public List<string> Diagnoses { get; set; } = [];

    // 5. Treatment provided

    public bool? FluorideDone { get; set; }

    public DateOnly? FluorideDate { get; set; }

    public string? FluorideOperator { get; set; }

    public string? FluorideToothNumbers { get; set; }

    public bool? SealantDone { get; set; }

    public DateOnly? SealantDate { get; set; }

    public string? SealantOperator { get; set; }

    public string? SealantToothNumbers { get; set; }

    public bool? SdfDone { get; set; }

    public DateOnly? SdfDate { get; set; }

    public string? SdfOperator { get; set; }

    public string? SdfToothNumbers { get; set; }

    public bool? ScalingDone { get; set; }

    public DateOnly? ScalingDate { get; set; }

    public string? ScalingOperator { get; set; }

    public string? ScalingToothNumbers { get; set; }

    // 6. Referral

    public DentalReferralType? ReferredFor { get; set; }

    public DentalReferralPriority? ReferralPriority { get; set; }

    public string? ReferralNotes { get; set; }

    public string? RecommendedFacility { get; set; }

    // 7. Remarks

    public string? Remarks { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public enum YesNoUnknown
{
    Yes,
    No,
    Unknown,
}

public enum BrushingFrequency
{
    Once,
    Twice,
    ThreeOrMore,
    Occasionally,
    Never,
}

public enum SugarConsumption
{
    Low,
    Moderate,
    High,
}

public enum AbsentPresent
{
    Absent,
    Present,
}

public enum FacialSymmetry
{
    Normal,
    Asymmetrical,
}

public enum Lymphadenopathy
{
    Absent,
    Present,
    NotAssessed,
}

public enum MouthOpening
{
    Normal,
    Limited,
}

public enum OralHygiene
{
    Good,
    Fair,
    Poor,
}

public enum PlaqueLevel
{
    None,
    Mild,
    Moderate,
    Heavy,
}

public enum GingivalCondition
{
    Healthy,
    Inflamed,
    Bleeding,
}

public enum CariesStatus
{
    Absent,
    NonCavitated,
    Cavitated,
}

public enum Malocclusion
{
    NotSuspected,
    Suspected,
}

public enum DentalReferralType
{
    Restorative,
    Extraction,
    Perio,
    Other,
    NotReferred,
}

public enum DentalReferralPriority
{
    Urgent,
    Early,
    Routine,
}
