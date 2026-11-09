using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Dental;

/// <summary>A visit's dental consultation.</summary>
public sealed record DentalConsultResponse
{
    public required int Id { get; init; }

    public required int VisitId { get; init; }

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

    public required IReadOnlyList<string> Diagnoses { get; init; }

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

    /// <summary>Id of the consultation's image; fetch its bytes from <c>GET /files/{id}/content</c>.</summary>
    public int? ImageFileId { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public static DentalConsultResponse From(DentalConsult d) =>
        new()
        {
            Id = d.Id,
            VisitId = d.VisitId,
            PreviousDentalVisit = d.PreviousDentalVisit,
            PreviousDentalTreatment = d.PreviousDentalTreatment,
            Toothache = d.Toothache,
            Extraction = d.Extraction,
            DentalTrauma = d.DentalTrauma,
            CurrentToothache = d.CurrentToothache,
            ToothSensitivity = d.ToothSensitivity,
            BleedingGums = d.BleedingGums,
            BrushingFrequency = d.BrushingFrequency,
            FluorideToothpaste = d.FluorideToothpaste,
            SugarConsumption = d.SugarConsumption,
            OtherDentalHistory = d.OtherDentalHistory,
            ChiefComplaint = d.ChiefComplaint,
            FacialSymmetry = d.FacialSymmetry,
            FacialSwelling = d.FacialSwelling,
            Lymphadenopathy = d.Lymphadenopathy,
            MouthOpening = d.MouthOpening,
            OralHygiene = d.OralHygiene,
            Plaque = d.Plaque,
            Calculus = d.Calculus,
            GingivalCondition = d.GingivalCondition,
            Caries = d.Caries,
            MissingTeeth = d.MissingTeeth,
            FracturedTeeth = d.FracturedTeeth,
            MucosalLesions = d.MucosalLesions,
            Malocclusion = d.Malocclusion,
            Diagnoses = d.Diagnoses,
            FluorideDone = d.FluorideDone,
            FluorideDate = d.FluorideDate,
            FluorideOperator = d.FluorideOperator,
            FluorideToothNumbers = d.FluorideToothNumbers,
            SealantDone = d.SealantDone,
            SealantDate = d.SealantDate,
            SealantOperator = d.SealantOperator,
            SealantToothNumbers = d.SealantToothNumbers,
            SdfDone = d.SdfDone,
            SdfDate = d.SdfDate,
            SdfOperator = d.SdfOperator,
            SdfToothNumbers = d.SdfToothNumbers,
            ScalingDone = d.ScalingDone,
            ScalingDate = d.ScalingDate,
            ScalingOperator = d.ScalingOperator,
            ScalingToothNumbers = d.ScalingToothNumbers,
            ReferredFor = d.ReferredFor,
            ReferralPriority = d.ReferralPriority,
            ReferralNotes = d.ReferralNotes,
            RecommendedFacility = d.RecommendedFacility,
            Remarks = d.Remarks,
            ImageFileId = d.ImageFileId,
            UpdatedAt = d.UpdatedAt,
        };
}
