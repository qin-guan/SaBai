using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Dental.UpsertByVisit.Put;

/// <summary>
/// The full dental consultation for a visit. It replaces whatever was saved before, so an
/// omitted field is cleared.
/// </summary>
public sealed record Request
{
    public Guid VisitId { get; init; }

    public YesNoUnknown? PreviousDentalVisit { get; init; }

    public YesNoUnknown? PreviousDentalTreatment { get; init; }

    public bool? Toothache { get; init; }

    public bool? Extraction { get; init; }

    public bool? DentalTrauma { get; init; }

    public bool? CurrentToothache { get; init; }

    public bool? ToothSensitivity { get; init; }

    public bool? BleedingGums { get; init; }

    public BrushingFrequency? BrushingFrequency { get; init; }

    public YesNoUnknown? FluorideToothpaste { get; init; }

    public SugarConsumption? SugarConsumption { get; init; }

    public string? OtherDentalHistory { get; init; }

    public string? ChiefComplaint { get; init; }

    public FacialSymmetry? FacialSymmetry { get; init; }

    public AbsentPresent? FacialSwelling { get; init; }

    public Lymphadenopathy? Lymphadenopathy { get; init; }

    public MouthOpening? MouthOpening { get; init; }

    public OralHygiene? OralHygiene { get; init; }

    public PlaqueLevel? Plaque { get; init; }

    public AbsentPresent? Calculus { get; init; }

    public GingivalCondition? GingivalCondition { get; init; }

    public CariesStatus? Caries { get; init; }

    public AbsentPresent? MissingTeeth { get; init; }

    public AbsentPresent? FracturedTeeth { get; init; }

    public AbsentPresent? MucosalLesions { get; init; }

    public Malocclusion? Malocclusion { get; init; }

    public IReadOnlyList<string>? Diagnoses { get; init; }

    public bool? FluorideDone { get; init; }

    public DateOnly? FluorideDate { get; init; }

    public string? FluorideOperator { get; init; }

    public string? FluorideToothNumbers { get; init; }

    public bool? SealantDone { get; init; }

    public DateOnly? SealantDate { get; init; }

    public string? SealantOperator { get; init; }

    public string? SealantToothNumbers { get; init; }

    public bool? SdfDone { get; init; }

    public DateOnly? SdfDate { get; init; }

    public string? SdfOperator { get; init; }

    public string? SdfToothNumbers { get; init; }

    public bool? ScalingDone { get; init; }

    public DateOnly? ScalingDate { get; init; }

    public string? ScalingOperator { get; init; }

    public string? ScalingToothNumbers { get; init; }

    public DentalReferralType? ReferredFor { get; init; }

    public DentalReferralPriority? ReferralPriority { get; init; }

    public string? ReferralNotes { get; init; }

    public string? RecommendedFacility { get; init; }

    public string? Remarks { get; init; }

    /// <summary>Id of a file uploaded through <c>POST /files</c> for this patient. Null clears it.</summary>
    public Guid? ImageFileId { get; init; }
}
