using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using SaBai.API.Domain;
using SaBai.Import.Upstream;

namespace SaBai.Import.Importing;

/// <summary>A row that cannot be mapped onto the SaBai model.</summary>
public sealed class MappingException(string message) : Exception(message);

/// <summary>
/// Pure field mapping from upstream rows onto SaBai entities. The same <c>Apply</c> methods are
/// used for new and existing rows, so a re-run with unchanged upstream data changes nothing.
/// </summary>
public static class UpstreamMapper
{
    /// <summary>Remark added to batches that were reserved upstream (see README, pharmacy).</summary>
    public const string ReservedRemark =
        "[Imported: reserved upstream for an order that could not be imported]";

    public static void Apply(UpVillageCode up, VillageCode target)
    {
        target.Code = Required(up.Code, "code");
        target.Name = Required(up.Name, "name");
        target.ColorHex = Required(up.ColorHex, "colorHex");
        target.IsVisible = up.IsVisible;
        target.CreatedAt = up.CreatedAt;
    }

    /// <param name="villageCodeId">Village of the patient's latest upstream visit, if any.</param>
    public static void Apply(UpPatient up, Patient target, Guid? villageCodeId, DateTimeOffset createdAt)
    {
        target.Name = Required(up.Name, "name").Trim();
        target.IdentificationNumber = Optional(up.IdentificationNumber);
        target.ContactNo = Optional(up.ContactNo);
        target.Gender = ParseGender(up.Gender);
        target.DateOfBirth = up.DateOfBirth;
        target.DrugAllergy = (up.DrugAllergy ?? "").Trim();
        target.HasPoorCard = up.HasPoorCard;
        target.HasBS2Card = up.HasBS2Card;
        target.HasSabaiCard = up.HasSabaiCard;
        target.VillageCodeId = villageCodeId;
        target.CreatedAt = createdAt;
    }

    public static void Apply(UpVisit up, Visit target, Guid villageCodeId)
    {
        target.PatientId = DeterministicIds.Patient(up.PatientId);
        target.VillageCodeId = villageCodeId;
        target.Date = up.Date;
    }

    public static void Apply(UpVitals up, Vitals target, DateTimeOffset visitDate)
    {
        target.VisitId = DeterministicIds.Visit(up.VisitId);
        target.Height = Decimal(up.Height, "height");
        target.Weight = Decimal(up.Weight, "weight");
        target.Temperature = Decimal(up.Temperature, "temperature");
        target.Systolic = up.Systolic;
        target.Diastolic = up.Diastolic;
        target.HeartRate = up.HeartRate;
        target.HemocueCount = Decimal(up.HemocueCount, "hemocueCount");
        target.DiabetesMellitus = up.DiabetesMellitus;
        target.UrineTest = up.UrineTest;
        target.BloodGlucoseNonFasting = Decimal(up.BloodGlucoseNonFasting, "bloodGlucoseNonFasting");
        target.BloodGlucoseFasting = Decimal(up.BloodGlucoseFasting, "bloodGlucoseFasting");
        target.Hba1c = Decimal(up.Hba1c, "hba1c");
        target.Others = up.Others;
        // Upstream does not record when vitals were taken; the visit date is the closest value.
        target.RecordedAt = visitDate;
    }

    public static void Apply(UpPuberty up, PubertyAssessment target)
    {
        target.VitalsId = DeterministicIds.Vitals(up.VitalId);
        target.Pubarche = up.Pubarche;
        target.PubarcheAge = up.PubarcheAge;
        target.Thelarche = up.Thelarche;
        target.ThelarcheAge = up.ThelarcheAge;
        target.Menarche = up.Menarche;
        target.MenarcheAge = up.MenarcheAge;
        target.VoiceChange = up.VoiceChange;
        target.VoiceChangeAge = up.VoiceChangeAge;
        target.TesticularGrowth = up.TesticularGrowth;
        target.TesticularGrowthAge = up.TesticularGrowthAge;
        target.AdditionalNotes = up.AdditionalNotes;
    }

    public static void Apply(UpEyesight up, Eyesight target, DateTimeOffset visitDate)
    {
        target.VisitId = DeterministicIds.Visit(up.VisitId);
        target.LeftEyeDegree = up.LeftEyeDegree;
        target.RightEyeDegree = up.RightEyeDegree;
        target.LeftEyePinhole = up.LeftEyePinhole;
        target.RightEyePinhole = up.RightEyePinhole;
        target.LeftAstigmatism = up.LeftAstigmatism;
        target.RightAstigmatism = up.RightAstigmatism;
        target.Comments = up.Comments;
        target.LeftPrescribedGlassesDegree = up.LeftPrescribedGlassesDegree;
        target.RightPrescribedGlassesDegree = up.RightPrescribedGlassesDegree;
        target.RecordedAt = visitDate;
    }

    public static void Apply(UpConsult up, Consult target, Guid doctorId)
    {
        target.VisitId = DeterministicIds.Visit(up.VisitId);
        target.Date = up.Date;
        target.PastMedicalHistory = up.PastMedicalHistory;
        target.Consultation = up.Consultation;
        target.TreatmentPlan = up.TreatmentPlan;
        target.Remarks = up.Remarks;
        target.DoctorId = doctorId;
    }

    public static void Apply(UpDiagnosis up, Diagnosis target)
    {
        var (category, details) = MapDiagnosis(up.Category, Required(up.Details, "details"));
        target.ConsultId = DeterministicIds.Consult(up.ConsultId);
        target.Category = category;
        target.Details = details;
    }

    public static void Apply(UpReferral up, Referral target)
    {
        target.ConsultId = DeterministicIds.Consult(up.ConsultId);
        target.ReferredFor = Required(up.ReferredFor, "referredFor");
        target.ReferralNotes = up.ReferralNotes;
        target.ReferralState = Enum.TryParse<ReferralState>(up.ReferralState, ignoreCase: false, out var state)
            && Enum.IsDefined(state)
            ? state
            : throw new MappingException($"unknown referral state '{up.ReferralState}'");
        target.ReferralOutcome = up.ReferralOutcome;
        target.CreatedAt = up.CreatedAt;
    }

    public static void Apply(UpReferralPartner up, ReferralPartner target)
    {
        target.Name = Required(up.Name, "name");
        target.Remarks = up.Remarks;
        target.IsActive = up.IsActive;
        target.CreatedAt = up.CreatedAt;
    }

    public static void Apply(UpIngredient up, MedicationActiveIngredient target)
    {
        target.Name = Required(up.Name, "name");
        target.UnitOfMeasurement = Required(up.UnitOfMeasurement, "unitOfMeasurement");
        target.FallBelow = up.FallBelow;
        target.Remarks = up.Remarks;
    }

    public static void Apply(UpBrand up, MedicationBrand target)
    {
        target.Name = Required(up.Name, "name");
        target.ActiveIngredientId = DeterministicIds.Ingredient(up.ActiveIngredientId);
        target.Remarks = up.Remarks;
    }

    public static void Apply(UpBroadcast up, Broadcast target, string? senderName)
    {
        target.Title = Required(up.Title, "title");
        target.Body = Required(up.Body, "body");
        target.Severity = up.Severity switch
        {
            "info" => BroadcastSeverity.Info,
            "warning" => BroadcastSeverity.Warning,
            "critical" => BroadcastSeverity.Critical,
            _ => throw new MappingException($"unknown broadcast severity '{up.Severity}'"),
        };
        target.CreatedBy = senderName;
        target.CreatedAt = up.CreatedAt;
        target.PublishedAt = up.PublishedAt;
        target.Attempts = up.Attempts;
    }

    /// <summary>What an upstream stock row becomes here.</summary>
    /// <returns>
    /// The status and remarks of the batch, or a reason to skip it: <c>dispensed</c> units have
    /// left the shelf (dispensing here takes them off the batch's quantity).
    /// </returns>
    public static (StockStatus Status, string? Remarks, string? SkipReason) MapStock(UpStock up)
    {
        if (up.Quantity < 0)
        {
            throw new MappingException($"negative quantity {up.Quantity}");
        }

        return up.StockStatus switch
        {
            "active" => (StockStatus.Active, up.Remarks, null),
            "discarded" => (StockStatus.Discarded, up.Remarks, null),
            "donated" => (StockStatus.Donated, up.Remarks, null),
            // Still on the shelf, but held for an upstream order that has no query route. The
            // units become ordinary active stock here so pending orders can be re-entered.
            "reserved" => (
                StockStatus.Active,
                string.IsNullOrWhiteSpace(up.Remarks) ? ReservedRemark : $"{up.Remarks.Trim()} {ReservedRemark}",
                null
            ),
            "dispensed" => (StockStatus.Active, null, "dispensed upstream: the units are no longer on hand"),
            _ => throw new MappingException($"unknown stock status '{up.StockStatus}'"),
        };
    }

    public static Gender ParseGender(string value) =>
        value switch
        {
            "male" => Gender.Male,
            "female" => Gender.Female,
            _ => throw new MappingException($"unknown gender '{value}'"),
        };

    private static readonly Dictionary<string, DiagnosisCategory> Categories = BuildCategories();

    /// <summary>
    /// Upstream stores the category as free text (validated against the same 15 names at
    /// creation). Unknown text maps to Others, with the original text kept in the details.
    /// </summary>
    public static (DiagnosisCategory? Category, string Details) MapDiagnosis(string? category, string details)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return (null, details);
        }

        return Categories.TryGetValue(category.Trim(), out var known)
            ? (known, details)
            : (DiagnosisCategory.Others, $"{details} (category: {category.Trim()})");
    }

    private static Dictionary<string, DiagnosisCategory> BuildCategories()
    {
        var map = new Dictionary<string, DiagnosisCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in typeof(DiagnosisCategory).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (DiagnosisCategory)field.GetValue(null)!;
            map[field.Name] = value;
            if (field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>() is { } attr)
            {
                map[attr.Name] = value;
            }
        }

        return map;
    }

    public static decimal? Decimal(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new MappingException($"{field} '{value}' is not a number");
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new MappingException($"{field} is empty") : value;

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
