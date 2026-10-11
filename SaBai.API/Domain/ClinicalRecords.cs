namespace SaBai.API.Domain;

public sealed class Vitals
{
    public int Id { get; set; }

    public int VisitId { get; set; }

    public Visit Visit { get; set; } = null!;

    public decimal? Height { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Temperature { get; set; }

    public int? Systolic { get; set; }

    public int? Diastolic { get; set; }

    public int? HeartRate { get; set; }

    public decimal? HemocueCount { get; set; }

    public bool? DiabetesMellitus { get; set; }

    public string? UrineTest { get; set; }

    public decimal? BloodGlucoseNonFasting { get; set; }

    public decimal? BloodGlucoseFasting { get; set; }

    public decimal? Hba1c { get; set; }

    public string? Others { get; set; }

    public PubertyAssessment? Puberty { get; set; }

    public ChildVitals? ChildVitals { get; set; }
}

public sealed class Eyesight
{
    public int Id { get; set; }

    public int VisitId { get; set; }

    public Visit Visit { get; set; } = null!;

    public string? LeftEyeDegree { get; set; }

    public string? RightEyeDegree { get; set; }

    public string? LeftEyePinhole { get; set; }

    public string? RightEyePinhole { get; set; }

    public string? LeftAstigmatism { get; set; }

    public string? RightAstigmatism { get; set; }

    public string? Comments { get; set; }

    public string? LeftPrescribedGlassesDegree { get; set; }

    public string? RightPrescribedGlassesDegree { get; set; }
}

public sealed class PubertyAssessment
{
    public int Id { get; set; }

    public int VitalsId { get; set; }

    public Vitals Vitals { get; set; } = null!;

    public bool? Pubarche { get; set; }

    public int? PubarcheAge { get; set; }

    public bool? Thelarche { get; set; }

    public int? ThelarcheAge { get; set; }

    public bool? Menarche { get; set; }

    public int? MenarcheAge { get; set; }

    public bool? VoiceChange { get; set; }

    public int? VoiceChangeAge { get; set; }

    public bool? TesticularGrowth { get; set; }

    public int? TesticularGrowthAge { get; set; }

    public string? AdditionalNotes { get; set; }
}

public sealed class ChildVitals
{
    public int Id { get; set; }

    public int VitalsId { get; set; }

    public Vitals Vitals { get; set; } = null!;

    public string? GrossMotor { get; set; }

    public string? RedReflex { get; set; }

    public string? Scoliosis { get; set; }

    public bool? Pallor { get; set; }

    public string? OralCavity { get; set; }

    public string? Heart { get; set; }

    public string? Abdomen { get; set; }

    public string? Lungs { get; set; }

    public string? HernialOrifices { get; set; }
}
