using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SaBai.API.Auth;
using SaBai.API.Data;
using SaBai.API.Domain;

namespace SaBai.API.Sync;

public sealed record SyncCrudOp(
    string Op,
    string Type,
    Guid Id,
    JsonElement? Data,
    long? OpId = null,
    int? BaseVersion = null
);

public sealed record SyncRejectionItem(
    long? OpId,
    string Table,
    Guid RecordId,
    string Reason
);

public sealed record SyncUploadResult(
    int Applied,
    int Conflicts,
    IReadOnlyList<SyncRejectionItem> Rejections
);

/// <summary>
/// Processes a PowerSync CRUD upload batch idempotently against <see cref="AppDbContext"/>.
/// Each operation runs inside its own savepoint so one invalid mutation is recorded in
/// <see cref="SyncRejection"/> without rolling back valid operations in the same batch.
/// </summary>
public sealed class SyncUploadProcessor(AppDbContext db)
{
    public async Task<SyncUploadResult> ProcessAsync(
        IReadOnlyList<SyncCrudOp> ops,
        ClaimsPrincipal user,
        CancellationToken ct
    )
    {
        var userId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)
            ? uid
            : Guid.Empty;
        var isAdmin = user.IsInRole(StaffRoles.Admin);
        // PN staff only work the referral desk: mirror the REST API, which lets them touch
        // referrals, referral partners and tags but nothing else clinical.
        var referralDeskOnly =
            user.IsInRole(StaffRoles.Pn) && !isAdmin && !user.IsInRole(StaffRoles.Member);

        var applied = 0;
        var conflictsBefore = await db.SyncConflicts.CountAsync(ct);
        var rejections = new List<SyncRejectionItem>();

        await using var tx = db.Database.IsNpgsql()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        for (var i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            var savepoint = $"sync_op_{i}";
            if (tx is not null)
            {
                await tx.CreateSavepointAsync(savepoint, ct);
            }

            try
            {
                if (referralDeskOnly && !ReferralDeskTables.Contains(op.Type))
                    throw new ForbiddenException($"PN staff cannot modify {op.Type}.");
                await ApplyOpAsync(op, userId, isAdmin, ct);
                await db.SaveChangesAsync(ct);
                if (tx is not null)
                {
                    await tx.ReleaseSavepointAsync(savepoint, ct);
                }
                applied++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (tx is not null)
                {
                    await tx.RollbackToSavepointAsync(savepoint, ct);
                }

                db.ChangeTracker.Clear();

                var reason = ex switch
                {
                    NotFoundException or ConflictException or BadRequestException or ForbiddenException => ex.Message,
                    DbUpdateException dbEx => dbEx.InnerException?.Message ?? dbEx.Message,
                    _ => ex.Message,
                };

                db.SyncRejections.Add(
                    new SyncRejection
                    {
                        Table = op.Type,
                        RecordId = op.Id,
                        Operation = op.Op.ToUpperInvariant(),
                        PayloadJson = op.Data?.GetRawText() ?? "{}",
                        Reason = reason,
                        UserId = userId == Guid.Empty ? null : userId,
                    }
                );
                await db.SaveChangesAsync(ct);
                rejections.Add(new SyncRejectionItem(op.OpId, op.Type, op.Id, reason));
            }
        }

        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        var conflictsAfter = await db.SyncConflicts.CountAsync(ct);
        return new SyncUploadResult(applied, Math.Max(0, conflictsAfter - conflictsBefore), rejections);
    }

    private static readonly HashSet<string> ReferralDeskTables =
    [
        "referrals",
        "referral_partners",
        "tags",
    ];

    private Task ApplyOpAsync(SyncCrudOp op, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var verb = op.Op.ToUpperInvariant();
        return op.Type switch
        {
            "village_codes" => ApplyVillageCodeAsync(verb, op, isAdmin, ct),
            "patients" => ApplyPatientAsync(verb, op, userId, ct),
            "visits" => ApplyVisitAsync(verb, op, userId, ct),
            "vitals" => ApplyVitalsAsync(verb, op, userId, ct),
            "puberty_assessments" => ApplyPubertyAsync(verb, op, userId, ct),
            "child_vitals" => ApplyChildVitalsAsync(verb, op, userId, ct),
            "eyesights" => ApplyEyesightAsync(verb, op, userId, ct),
            "dental_consults" => ApplyDentalAsync(verb, op, userId, ct),
            "consults" => ApplyConsultAsync(verb, op, userId, ct),
            "diagnoses" => ApplyDiagnosisAsync(verb, op, userId, ct),
            "referrals" => ApplyReferralAsync(verb, op, userId, ct),
            "referral_partners" => ApplyReferralPartnerAsync(verb, op, userId, ct),
            "tags" => ApplyTagAsync(verb, op, ct),
            "patient_tags" => ApplyPatientTagAsync(verb, op, ct),
            "medication_active_ingredients" => ApplyIngredientAsync(verb, op, userId, ct),
            "medication_brands" => ApplyBrandAsync(verb, op, userId, ct),
            "medication_stocks" => ApplyStockAsync(verb, op, userId, ct),
            "stock_changes" => ApplyStockChangeAsync(verb, op, userId, ct),
            "orders" => ApplyOrderAsync(verb, op, userId, ct),
            _ => throw new BadRequestException($"Unsupported sync table: {op.Type}"),
        };
    }

    private async Task ApplyVillageCodeAsync(
        string verb,
        SyncCrudOp op,
        bool isAdmin,
        CancellationToken ct
    )
    {
        if (!isAdmin)
            throw new ForbiddenException("Only admins can modify village codes.");

        if (verb == "DELETE")
        {
            var existing = await db.VillageCodes.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.VillageCodes.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.VillageCodes.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.VillageCodes.Add(
                new VillageCode
                {
                    Id = op.Id,
                    Code = RequireString(d, "code"),
                    Name = RequireString(d, "name"),
                    ColorHex = GetString(d, "color_hex") ?? "#1f3a5f",
                    IsVisible = GetBool(d, "is_visible") ?? true,
                    CreatedAt = GetDateTimeOffset(d, "created_at") ?? DateTimeOffset.UtcNow,
                }
            );
            return;
        }

        if (Has(d, "code"))
            row.Code = RequireString(d, "code");
        if (Has(d, "name"))
            row.Name = RequireString(d, "name");
        if (Has(d, "color_hex"))
            row.ColorHex = GetString(d, "color_hex") ?? row.ColorHex;
        if (Has(d, "is_visible"))
            row.IsVisible = GetBool(d, "is_visible") ?? row.IsVisible;
    }

    private async Task ApplyPatientAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.Patients.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.Patients.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.Patients.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.Patients.Add(
                new Patient
                {
                    Id = op.Id,
                    Name = RequireString(d, "name"),
                    VillageCodeId = GetGuid(d, "village_code_id"),
                    IdentificationNumber = GetString(d, "identification_number"),
                    ContactNo = GetString(d, "contact_no"),
                    Gender = ParseEnum<Gender>(GetString(d, "gender")) ?? Gender.Female,
                    DateOfBirth = GetDateTimeOffset(d, "date_of_birth") ?? DateTimeOffset.UtcNow,
                    DrugAllergy = GetString(d, "drug_allergy") ?? "None",
                    HasPoorCard = GetBool(d, "has_poor_card") ?? false,
                    HasBS2Card = GetBool(d, "has_bs2_card") ?? false,
                    HasSabaiCard = GetBool(d, "has_sabai_card") ?? false,
                    ToGetReport = GetBool(d, "to_get_report") ?? false,
                    CreatedAt = GetDateTimeOffset(d, "created_at") ?? DateTimeOffset.UtcNow,
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "patients", row.Id, "name", row.Name, GetString(d, "name"), Has(d, "name"), v => row.Name = v ?? row.Name, userId);
        SetField(logConflict, "patients", row.Id, "village_code_id", row.VillageCodeId, GetGuid(d, "village_code_id"), Has(d, "village_code_id"), v => row.VillageCodeId = v, userId);
        SetField(logConflict, "patients", row.Id, "identification_number", row.IdentificationNumber, GetString(d, "identification_number"), Has(d, "identification_number"), v => row.IdentificationNumber = v, userId);
        SetField(logConflict, "patients", row.Id, "contact_no", row.ContactNo, GetString(d, "contact_no"), Has(d, "contact_no"), v => row.ContactNo = v, userId);
        SetField(logConflict, "patients", row.Id, "gender", row.Gender, ParseEnum<Gender>(GetString(d, "gender")) ?? row.Gender, Has(d, "gender"), v => row.Gender = v, userId);
        SetField(logConflict, "patients", row.Id, "date_of_birth", row.DateOfBirth, GetDateTimeOffset(d, "date_of_birth") ?? row.DateOfBirth, Has(d, "date_of_birth"), v => row.DateOfBirth = v, userId);
        SetField(logConflict, "patients", row.Id, "drug_allergy", row.DrugAllergy, GetString(d, "drug_allergy") ?? row.DrugAllergy, Has(d, "drug_allergy"), v => row.DrugAllergy = v, userId);
        SetField(logConflict, "patients", row.Id, "has_poor_card", row.HasPoorCard, GetBool(d, "has_poor_card") ?? row.HasPoorCard, Has(d, "has_poor_card"), v => row.HasPoorCard = v, userId);
        SetField(logConflict, "patients", row.Id, "has_bs2_card", row.HasBS2Card, GetBool(d, "has_bs2_card") ?? row.HasBS2Card, Has(d, "has_bs2_card"), v => row.HasBS2Card = v, userId);
        SetField(logConflict, "patients", row.Id, "has_sabai_card", row.HasSabaiCard, GetBool(d, "has_sabai_card") ?? row.HasSabaiCard, Has(d, "has_sabai_card"), v => row.HasSabaiCard = v, userId);
        SetField(logConflict, "patients", row.Id, "to_get_report", row.ToGetReport, GetBool(d, "to_get_report") ?? row.ToGetReport, Has(d, "to_get_report"), v => row.ToGetReport = v, userId);
    }

    private async Task ApplyVisitAsync(string verb, SyncCrudOp op, Guid userId, CancellationToken ct)
    {
        if (verb == "DELETE")
        {
            var existing = await db.Visits.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.Visits.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.Visits.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.Visits.Add(
                new Visit
                {
                    Id = op.Id,
                    PatientId = RequireGuid(d, "patient_id"),
                    VillageCodeId = RequireGuid(d, "village_code_id"),
                    Date = GetDateTimeOffset(d, "date") ?? DateTimeOffset.UtcNow,
                    Status = GetString(d, "status") ?? Visit.StartedStatus,
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "visits", row.Id, "status", row.Status, GetString(d, "status") ?? row.Status, Has(d, "status"), v => row.Status = v, userId);
        SetField(logConflict, "visits", row.Id, "village_code_id", row.VillageCodeId, GetGuid(d, "village_code_id") ?? row.VillageCodeId, Has(d, "village_code_id"), v => row.VillageCodeId = v, userId);
    }

    private async Task ApplyVitalsAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.Vitals.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.Vitals.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var visitId = GetGuid(d, "visit_id");
        // Natural-key 1:1 upsert: if another device already created Vitals for this visit,
        // merge into the existing row instead of failing on the unique VisitId index.
        var row =
            await db.Vitals.FirstOrDefaultAsync(x => x.Id == op.Id, ct)
            ?? (visitId is { } vid
                ? await db.Vitals.FirstOrDefaultAsync(x => x.VisitId == vid, ct)
                : null);

        if (row is null)
        {
            db.Vitals.Add(
                new Vitals
                {
                    Id = op.Id,
                    VisitId = visitId ?? throw new BadRequestException("visit_id is required."),
                    RecordedAt = GetDateTimeOffset(d, "recorded_at") ?? DateTimeOffset.UtcNow,
                    Height = GetDecimal(d, "height"),
                    Weight = GetDecimal(d, "weight"),
                    Temperature = GetDecimal(d, "temperature"),
                    Systolic = GetInt(d, "systolic"),
                    Diastolic = GetInt(d, "diastolic"),
                    HeartRate = GetInt(d, "heart_rate"),
                    HemocueCount = GetDecimal(d, "hemocue_count"),
                    DiabetesMellitus = GetBool(d, "diabetes_mellitus"),
                    UrineTest = GetString(d, "urine_test"),
                    BloodGlucoseNonFasting = GetDecimal(d, "blood_glucose_non_fasting"),
                    BloodGlucoseFasting = GetDecimal(d, "blood_glucose_fasting"),
                    Hba1c = GetDecimal(d, "hba1c"),
                    Others = GetString(d, "others"),
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version) || row.Id != op.Id;
        SetField(logConflict, "vitals", row.Id, "height", row.Height, GetDecimal(d, "height"), Has(d, "height"), v => row.Height = v, userId);
        SetField(logConflict, "vitals", row.Id, "weight", row.Weight, GetDecimal(d, "weight"), Has(d, "weight"), v => row.Weight = v, userId);
        SetField(logConflict, "vitals", row.Id, "temperature", row.Temperature, GetDecimal(d, "temperature"), Has(d, "temperature"), v => row.Temperature = v, userId);
        SetField(logConflict, "vitals", row.Id, "systolic", row.Systolic, GetInt(d, "systolic"), Has(d, "systolic"), v => row.Systolic = v, userId);
        SetField(logConflict, "vitals", row.Id, "diastolic", row.Diastolic, GetInt(d, "diastolic"), Has(d, "diastolic"), v => row.Diastolic = v, userId);
        SetField(logConflict, "vitals", row.Id, "heart_rate", row.HeartRate, GetInt(d, "heart_rate"), Has(d, "heart_rate"), v => row.HeartRate = v, userId);
        SetField(logConflict, "vitals", row.Id, "hemocue_count", row.HemocueCount, GetDecimal(d, "hemocue_count"), Has(d, "hemocue_count"), v => row.HemocueCount = v, userId);
        SetField(logConflict, "vitals", row.Id, "diabetes_mellitus", row.DiabetesMellitus, GetBool(d, "diabetes_mellitus"), Has(d, "diabetes_mellitus"), v => row.DiabetesMellitus = v, userId);
        SetField(logConflict, "vitals", row.Id, "urine_test", row.UrineTest, GetString(d, "urine_test"), Has(d, "urine_test"), v => row.UrineTest = v, userId);
        SetField(logConflict, "vitals", row.Id, "blood_glucose_non_fasting", row.BloodGlucoseNonFasting, GetDecimal(d, "blood_glucose_non_fasting"), Has(d, "blood_glucose_non_fasting"), v => row.BloodGlucoseNonFasting = v, userId);
        SetField(logConflict, "vitals", row.Id, "blood_glucose_fasting", row.BloodGlucoseFasting, GetDecimal(d, "blood_glucose_fasting"), Has(d, "blood_glucose_fasting"), v => row.BloodGlucoseFasting = v, userId);
        SetField(logConflict, "vitals", row.Id, "hba1c", row.Hba1c, GetDecimal(d, "hba1c"), Has(d, "hba1c"), v => row.Hba1c = v, userId);
        SetField(logConflict, "vitals", row.Id, "others", row.Others, GetString(d, "others"), Has(d, "others"), v => row.Others = v, userId);
    }

    private async Task ApplyPubertyAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.PubertyAssessments.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.PubertyAssessments.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var vitalsId = GetGuid(d, "vitals_id");
        var row =
            await db.PubertyAssessments.FirstOrDefaultAsync(x => x.Id == op.Id, ct)
            ?? (vitalsId is { } vid
                ? await db.PubertyAssessments.FirstOrDefaultAsync(x => x.VitalsId == vid, ct)
                : null);

        if (row is null)
        {
            db.PubertyAssessments.Add(
                new PubertyAssessment
                {
                    Id = op.Id,
                    VitalsId = vitalsId ?? throw new BadRequestException("vitals_id is required."),
                    Pubarche = GetBool(d, "pubarche"),
                    PubarcheAge = GetInt(d, "pubarche_age"),
                    Thelarche = GetBool(d, "thelarche"),
                    ThelarcheAge = GetInt(d, "thelarche_age"),
                    Menarche = GetBool(d, "menarche"),
                    MenarcheAge = GetInt(d, "menarche_age"),
                    VoiceChange = GetBool(d, "voice_change"),
                    VoiceChangeAge = GetInt(d, "voice_change_age"),
                    TesticularGrowth = GetBool(d, "testicular_growth"),
                    TesticularGrowthAge = GetInt(d, "testicular_growth_age"),
                    AdditionalNotes = GetString(d, "additional_notes"),
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version) || row.Id != op.Id;
        SetField(logConflict, "puberty_assessments", row.Id, "pubarche", row.Pubarche, GetBool(d, "pubarche"), Has(d, "pubarche"), v => row.Pubarche = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "pubarche_age", row.PubarcheAge, GetInt(d, "pubarche_age"), Has(d, "pubarche_age"), v => row.PubarcheAge = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "thelarche", row.Thelarche, GetBool(d, "thelarche"), Has(d, "thelarche"), v => row.Thelarche = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "thelarche_age", row.ThelarcheAge, GetInt(d, "thelarche_age"), Has(d, "thelarche_age"), v => row.ThelarcheAge = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "menarche", row.Menarche, GetBool(d, "menarche"), Has(d, "menarche"), v => row.Menarche = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "menarche_age", row.MenarcheAge, GetInt(d, "menarche_age"), Has(d, "menarche_age"), v => row.MenarcheAge = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "voice_change", row.VoiceChange, GetBool(d, "voice_change"), Has(d, "voice_change"), v => row.VoiceChange = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "voice_change_age", row.VoiceChangeAge, GetInt(d, "voice_change_age"), Has(d, "voice_change_age"), v => row.VoiceChangeAge = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "testicular_growth", row.TesticularGrowth, GetBool(d, "testicular_growth"), Has(d, "testicular_growth"), v => row.TesticularGrowth = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "testicular_growth_age", row.TesticularGrowthAge, GetInt(d, "testicular_growth_age"), Has(d, "testicular_growth_age"), v => row.TesticularGrowthAge = v, userId);
        SetField(logConflict, "puberty_assessments", row.Id, "additional_notes", row.AdditionalNotes, GetString(d, "additional_notes"), Has(d, "additional_notes"), v => row.AdditionalNotes = v, userId);
    }

    private async Task ApplyChildVitalsAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.ChildVitals.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.ChildVitals.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var vitalsId = GetGuid(d, "vitals_id");
        var row =
            await db.ChildVitals.FirstOrDefaultAsync(x => x.Id == op.Id, ct)
            ?? (vitalsId is { } vid
                ? await db.ChildVitals.FirstOrDefaultAsync(x => x.VitalsId == vid, ct)
                : null);

        if (row is null)
        {
            db.ChildVitals.Add(
                new ChildVitals
                {
                    Id = op.Id,
                    VitalsId = vitalsId ?? throw new BadRequestException("vitals_id is required."),
                    GrossMotor = GetString(d, "gross_motor"),
                    RedReflex = GetString(d, "red_reflex"),
                    Scoliosis = GetString(d, "scoliosis"),
                    Pallor = GetBool(d, "pallor"),
                    OralCavity = GetString(d, "oral_cavity"),
                    Heart = GetString(d, "heart"),
                    Abdomen = GetString(d, "abdomen"),
                    Lungs = GetString(d, "lungs"),
                    HernialOrifices = GetString(d, "hernial_orifices"),
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version) || row.Id != op.Id;
        SetField(logConflict, "child_vitals", row.Id, "gross_motor", row.GrossMotor, GetString(d, "gross_motor"), Has(d, "gross_motor"), v => row.GrossMotor = v, userId);
        SetField(logConflict, "child_vitals", row.Id, "red_reflex", row.RedReflex, GetString(d, "red_reflex"), Has(d, "red_reflex"), v => row.RedReflex = v, userId);
        SetField(logConflict, "child_vitals", row.Id, "scoliosis", row.Scoliosis, GetString(d, "scoliosis"), Has(d, "scoliosis"), v => row.Scoliosis = v, userId);
        SetField(logConflict, "child_vitals", row.Id, "pallor", row.Pallor, GetBool(d, "pallor"), Has(d, "pallor"), v => row.Pallor = v, userId);
        SetField(logConflict, "child_vitals", row.Id, "oral_cavity", row.OralCavity, GetString(d, "oral_cavity"), Has(d, "oral_cavity"), v => row.OralCavity = v, userId);
        SetField(logConflict, "child_vitals", row.Id, "heart", row.Heart, GetString(d, "heart"), Has(d, "heart"), v => row.Heart = v, userId);
        SetField(logConflict, "child_vitals", row.Id, "abdomen", row.Abdomen, GetString(d, "abdomen"), Has(d, "abdomen"), v => row.Abdomen = v, userId);
        SetField(logConflict, "child_vitals", row.Id, "lungs", row.Lungs, GetString(d, "lungs"), Has(d, "lungs"), v => row.Lungs = v, userId);
        SetField(logConflict, "child_vitals", row.Id, "hernial_orifices", row.HernialOrifices, GetString(d, "hernial_orifices"), Has(d, "hernial_orifices"), v => row.HernialOrifices = v, userId);
    }

    private async Task ApplyEyesightAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.Eyesights.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.Eyesights.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var visitId = GetGuid(d, "visit_id");
        var row =
            await db.Eyesights.FirstOrDefaultAsync(x => x.Id == op.Id, ct)
            ?? (visitId is { } vid
                ? await db.Eyesights.FirstOrDefaultAsync(x => x.VisitId == vid, ct)
                : null);

        if (row is null)
        {
            row = new Eyesight
            {
                Id = op.Id,
                VisitId = visitId ?? throw new BadRequestException("visit_id is required."),
                RecordedAt = GetDateTimeOffset(d, "recorded_at") ?? DateTimeOffset.UtcNow,
                LeftEyeDegree = EyesightRules.NormalizeAcuity(GetString(d, "left_eye_degree")),
                RightEyeDegree = EyesightRules.NormalizeAcuity(GetString(d, "right_eye_degree")),
                LeftEyePinhole = EyesightRules.NormalizePinhole(GetString(d, "left_eye_pinhole")),
                RightEyePinhole = EyesightRules.NormalizePinhole(GetString(d, "right_eye_pinhole")),
                LeftAstigmatism = GetString(d, "left_astigmatism"),
                RightAstigmatism = GetString(d, "right_astigmatism"),
                Comments = GetString(d, "comments"),
                LeftPrescribedGlassesDegree = GetString(d, "left_prescribed_glasses_degree"),
                RightPrescribedGlassesDegree = GetString(d, "right_prescribed_glasses_degree"),
            };
            db.Eyesights.Add(row);
            return;
        }

        var logConflict = IsStale(op, d, row.Version) || row.Id != op.Id;
        SetField(logConflict, "eyesights", row.Id, "left_eye_degree", row.LeftEyeDegree, EyesightRules.NormalizeAcuity(GetString(d, "left_eye_degree")), Has(d, "left_eye_degree"), v => row.LeftEyeDegree = v, userId);
        SetField(logConflict, "eyesights", row.Id, "right_eye_degree", row.RightEyeDegree, EyesightRules.NormalizeAcuity(GetString(d, "right_eye_degree")), Has(d, "right_eye_degree"), v => row.RightEyeDegree = v, userId);
        SetField(logConflict, "eyesights", row.Id, "left_eye_pinhole", row.LeftEyePinhole, EyesightRules.NormalizePinhole(GetString(d, "left_eye_pinhole")), Has(d, "left_eye_pinhole"), v => row.LeftEyePinhole = v, userId);
        SetField(logConflict, "eyesights", row.Id, "right_eye_pinhole", row.RightEyePinhole, EyesightRules.NormalizePinhole(GetString(d, "right_eye_pinhole")), Has(d, "right_eye_pinhole"), v => row.RightEyePinhole = v, userId);
        SetField(logConflict, "eyesights", row.Id, "left_astigmatism", row.LeftAstigmatism, GetString(d, "left_astigmatism"), Has(d, "left_astigmatism"), v => row.LeftAstigmatism = v, userId);
        SetField(logConflict, "eyesights", row.Id, "right_astigmatism", row.RightAstigmatism, GetString(d, "right_astigmatism"), Has(d, "right_astigmatism"), v => row.RightAstigmatism = v, userId);
        SetField(logConflict, "eyesights", row.Id, "comments", row.Comments, GetString(d, "comments"), Has(d, "comments"), v => row.Comments = v, userId);
        SetField(logConflict, "eyesights", row.Id, "left_prescribed_glasses_degree", row.LeftPrescribedGlassesDegree, GetString(d, "left_prescribed_glasses_degree"), Has(d, "left_prescribed_glasses_degree"), v => row.LeftPrescribedGlassesDegree = v, userId);
        SetField(logConflict, "eyesights", row.Id, "right_prescribed_glasses_degree", row.RightPrescribedGlassesDegree, GetString(d, "right_prescribed_glasses_degree"), Has(d, "right_prescribed_glasses_degree"), v => row.RightPrescribedGlassesDegree = v, userId);
    }

    private async Task ApplyDentalAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.DentalConsults.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.DentalConsults.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var visitId = GetGuid(d, "visit_id");
        var row =
            await db.DentalConsults.FirstOrDefaultAsync(x => x.Id == op.Id, ct)
            ?? (visitId is { } vid
                ? await db.DentalConsults.FirstOrDefaultAsync(x => x.VisitId == vid, ct)
                : null);

        if (row is null)
        {
            row = new DentalConsult
            {
                Id = op.Id,
                VisitId = visitId ?? throw new BadRequestException("visit_id is required."),
                RecordedAt = GetDateTimeOffset(d, "recorded_at") ?? DateTimeOffset.UtcNow,
            };
            ApplyDentalFields(row, d, false, userId);
            db.DentalConsults.Add(row);
            return;
        }

        var logConflict = IsStale(op, d, row.Version) || row.Id != op.Id;
        ApplyDentalFields(row, d, logConflict, userId);
    }

    private void ApplyDentalFields(DentalConsult row, JsonElement d, bool logConflict, Guid userId)
    {
        SetField(logConflict, "dental_consults", row.Id, "previous_dental_visit", row.PreviousDentalVisit, ParseEnum<YesNoUnknown>(GetString(d, "previous_dental_visit")), Has(d, "previous_dental_visit"), v => row.PreviousDentalVisit = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "previous_dental_treatment", row.PreviousDentalTreatment, ParseEnum<YesNoUnknown>(GetString(d, "previous_dental_treatment")), Has(d, "previous_dental_treatment"), v => row.PreviousDentalTreatment = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "toothache", row.Toothache, GetBool(d, "toothache"), Has(d, "toothache"), v => row.Toothache = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "extraction", row.Extraction, GetBool(d, "extraction"), Has(d, "extraction"), v => row.Extraction = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "dental_trauma", row.DentalTrauma, GetBool(d, "dental_trauma"), Has(d, "dental_trauma"), v => row.DentalTrauma = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "current_toothache", row.CurrentToothache, GetBool(d, "current_toothache"), Has(d, "current_toothache"), v => row.CurrentToothache = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "tooth_sensitivity", row.ToothSensitivity, GetBool(d, "tooth_sensitivity"), Has(d, "tooth_sensitivity"), v => row.ToothSensitivity = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "bleeding_gums", row.BleedingGums, GetBool(d, "bleeding_gums"), Has(d, "bleeding_gums"), v => row.BleedingGums = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "brushing_frequency", row.BrushingFrequency, ParseEnum<BrushingFrequency>(GetString(d, "brushing_frequency")), Has(d, "brushing_frequency"), v => row.BrushingFrequency = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "fluoride_toothpaste", row.FluorideToothpaste, ParseEnum<YesNoUnknown>(GetString(d, "fluoride_toothpaste")), Has(d, "fluoride_toothpaste"), v => row.FluorideToothpaste = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sugar_consumption", row.SugarConsumption, ParseEnum<SugarConsumption>(GetString(d, "sugar_consumption")), Has(d, "sugar_consumption"), v => row.SugarConsumption = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "other_dental_history", row.OtherDentalHistory, GetString(d, "other_dental_history"), Has(d, "other_dental_history"), v => row.OtherDentalHistory = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "chief_complaint", row.ChiefComplaint, GetString(d, "chief_complaint"), Has(d, "chief_complaint"), v => row.ChiefComplaint = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "facial_symmetry", row.FacialSymmetry, ParseEnum<FacialSymmetry>(GetString(d, "facial_symmetry")), Has(d, "facial_symmetry"), v => row.FacialSymmetry = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "facial_swelling", row.FacialSwelling, ParseEnum<AbsentPresent>(GetString(d, "facial_swelling")), Has(d, "facial_swelling"), v => row.FacialSwelling = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "lymphadenopathy", row.Lymphadenopathy, ParseEnum<Lymphadenopathy>(GetString(d, "lymphadenopathy")), Has(d, "lymphadenopathy"), v => row.Lymphadenopathy = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "mouth_opening", row.MouthOpening, ParseEnum<MouthOpening>(GetString(d, "mouth_opening")), Has(d, "mouth_opening"), v => row.MouthOpening = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "oral_hygiene", row.OralHygiene, ParseEnum<OralHygiene>(GetString(d, "oral_hygiene")), Has(d, "oral_hygiene"), v => row.OralHygiene = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "plaque", row.Plaque, ParseEnum<PlaqueLevel>(GetString(d, "plaque")), Has(d, "plaque"), v => row.Plaque = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "calculus", row.Calculus, ParseEnum<AbsentPresent>(GetString(d, "calculus")), Has(d, "calculus"), v => row.Calculus = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "gingival_condition", row.GingivalCondition, ParseEnum<GingivalCondition>(GetString(d, "gingival_condition")), Has(d, "gingival_condition"), v => row.GingivalCondition = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "caries", row.Caries, ParseEnum<CariesStatus>(GetString(d, "caries")), Has(d, "caries"), v => row.Caries = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "missing_teeth", row.MissingTeeth, ParseEnum<AbsentPresent>(GetString(d, "missing_teeth")), Has(d, "missing_teeth"), v => row.MissingTeeth = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "fractured_teeth", row.FracturedTeeth, ParseEnum<AbsentPresent>(GetString(d, "fractured_teeth")), Has(d, "fractured_teeth"), v => row.FracturedTeeth = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "mucosal_lesions", row.MucosalLesions, ParseEnum<AbsentPresent>(GetString(d, "mucosal_lesions")), Has(d, "mucosal_lesions"), v => row.MucosalLesions = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "malocclusion", row.Malocclusion, ParseEnum<Malocclusion>(GetString(d, "malocclusion")), Has(d, "malocclusion"), v => row.Malocclusion = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "diagnoses", row.Diagnoses, GetStringList(d, "diagnoses") ?? row.Diagnoses, Has(d, "diagnoses"), v => row.Diagnoses = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "fluoride_done", row.FluorideDone, GetBool(d, "fluoride_done"), Has(d, "fluoride_done"), v => row.FluorideDone = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "fluoride_date", row.FluorideDate, GetDateOnly(d, "fluoride_date"), Has(d, "fluoride_date"), v => row.FluorideDate = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "fluoride_operator", row.FluorideOperator, GetString(d, "fluoride_operator"), Has(d, "fluoride_operator"), v => row.FluorideOperator = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "fluoride_tooth_numbers", row.FluorideToothNumbers, GetString(d, "fluoride_tooth_numbers"), Has(d, "fluoride_tooth_numbers"), v => row.FluorideToothNumbers = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sealant_done", row.SealantDone, GetBool(d, "sealant_done"), Has(d, "sealant_done"), v => row.SealantDone = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sealant_date", row.SealantDate, GetDateOnly(d, "sealant_date"), Has(d, "sealant_date"), v => row.SealantDate = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sealant_operator", row.SealantOperator, GetString(d, "sealant_operator"), Has(d, "sealant_operator"), v => row.SealantOperator = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sealant_tooth_numbers", row.SealantToothNumbers, GetString(d, "sealant_tooth_numbers"), Has(d, "sealant_tooth_numbers"), v => row.SealantToothNumbers = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sdf_done", row.SdfDone, GetBool(d, "sdf_done"), Has(d, "sdf_done"), v => row.SdfDone = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sdf_date", row.SdfDate, GetDateOnly(d, "sdf_date"), Has(d, "sdf_date"), v => row.SdfDate = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sdf_operator", row.SdfOperator, GetString(d, "sdf_operator"), Has(d, "sdf_operator"), v => row.SdfOperator = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "sdf_tooth_numbers", row.SdfToothNumbers, GetString(d, "sdf_tooth_numbers"), Has(d, "sdf_tooth_numbers"), v => row.SdfToothNumbers = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "scaling_done", row.ScalingDone, GetBool(d, "scaling_done"), Has(d, "scaling_done"), v => row.ScalingDone = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "scaling_date", row.ScalingDate, GetDateOnly(d, "scaling_date"), Has(d, "scaling_date"), v => row.ScalingDate = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "scaling_operator", row.ScalingOperator, GetString(d, "scaling_operator"), Has(d, "scaling_operator"), v => row.ScalingOperator = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "scaling_tooth_numbers", row.ScalingToothNumbers, GetString(d, "scaling_tooth_numbers"), Has(d, "scaling_tooth_numbers"), v => row.ScalingToothNumbers = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "referred_for", row.ReferredFor, ParseEnum<DentalReferralType>(GetString(d, "referred_for")), Has(d, "referred_for"), v => row.ReferredFor = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "referral_priority", row.ReferralPriority, ParseEnum<DentalReferralPriority>(GetString(d, "referral_priority")), Has(d, "referral_priority"), v => row.ReferralPriority = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "referral_notes", row.ReferralNotes, GetString(d, "referral_notes"), Has(d, "referral_notes"), v => row.ReferralNotes = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "recommended_facility", row.RecommendedFacility, GetString(d, "recommended_facility"), Has(d, "recommended_facility"), v => row.RecommendedFacility = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "remarks", row.Remarks, GetString(d, "remarks"), Has(d, "remarks"), v => row.Remarks = v, userId);
        SetField(logConflict, "dental_consults", row.Id, "image_file_id", row.ImageFileId, GetGuid(d, "image_file_id"), Has(d, "image_file_id"), v => row.ImageFileId = v, userId);
    }

    private async Task ApplyConsultAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.Consults.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is null)
                return;
            if (existing.IsEditLocked(DateTimeOffset.UtcNow))
                throw new ForbiddenException("Consult is past the 24-hour edit window.");
            db.Consults.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.Consults.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.Consults.Add(
                new Consult
                {
                    Id = op.Id,
                    VisitId = RequireGuid(d, "visit_id"),
                    DoctorId = GetGuid(d, "doctor_id") ?? userId,
                    Date = GetDateTimeOffset(d, "date") ?? DateTimeOffset.UtcNow,
                    PastMedicalHistory = GetString(d, "past_medical_history"),
                    Consultation = GetString(d, "consultation"),
                    TreatmentPlan = GetString(d, "treatment_plan"),
                    ReferredFor = GetString(d, "referred_for"),
                    ReferralNotes = GetString(d, "referral_notes"),
                    Remarks = GetString(d, "remarks"),
                }
            );
            return;
        }

        if (row.IsEditLocked(DateTimeOffset.UtcNow))
            throw new ForbiddenException("Consult is past the 24-hour edit window.");

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "consults", row.Id, "past_medical_history", row.PastMedicalHistory, GetString(d, "past_medical_history"), Has(d, "past_medical_history"), v => row.PastMedicalHistory = v, userId);
        SetField(logConflict, "consults", row.Id, "consultation", row.Consultation, GetString(d, "consultation"), Has(d, "consultation"), v => row.Consultation = v, userId);
        SetField(logConflict, "consults", row.Id, "treatment_plan", row.TreatmentPlan, GetString(d, "treatment_plan"), Has(d, "treatment_plan"), v => row.TreatmentPlan = v, userId);
        SetField(logConflict, "consults", row.Id, "referred_for", row.ReferredFor, GetString(d, "referred_for"), Has(d, "referred_for"), v => row.ReferredFor = v, userId);
        SetField(logConflict, "consults", row.Id, "referral_notes", row.ReferralNotes, GetString(d, "referral_notes"), Has(d, "referral_notes"), v => row.ReferralNotes = v, userId);
        SetField(logConflict, "consults", row.Id, "remarks", row.Remarks, GetString(d, "remarks"), Has(d, "remarks"), v => row.Remarks = v, userId);
    }

    private async Task ApplyDiagnosisAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.Diagnoses.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.Diagnoses.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.Diagnoses.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.Diagnoses.Add(
                new Diagnosis
                {
                    Id = op.Id,
                    ConsultId = RequireGuid(d, "consult_id"),
                    Details = RequireString(d, "details"),
                    Category = ParseEnum<DiagnosisCategory>(GetString(d, "category")),
                    CodingSystem = GetString(d, "coding_system"),
                    Code = GetString(d, "code"),
                    CodeDisplay = GetString(d, "code_display"),
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "diagnoses", row.Id, "details", row.Details, GetString(d, "details") ?? row.Details, Has(d, "details"), v => row.Details = v, userId);
        SetField(logConflict, "diagnoses", row.Id, "category", row.Category, ParseEnum<DiagnosisCategory>(GetString(d, "category")), Has(d, "category"), v => row.Category = v, userId);
        SetField(logConflict, "diagnoses", row.Id, "coding_system", row.CodingSystem, GetString(d, "coding_system"), Has(d, "coding_system"), v => row.CodingSystem = v, userId);
        SetField(logConflict, "diagnoses", row.Id, "code", row.Code, GetString(d, "code"), Has(d, "code"), v => row.Code = v, userId);
        SetField(logConflict, "diagnoses", row.Id, "code_display", row.CodeDisplay, GetString(d, "code_display"), Has(d, "code_display"), v => row.CodeDisplay = v, userId);
    }

    private async Task ApplyReferralAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.Referrals.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.Referrals.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.Referrals.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.Referrals.Add(
                new Referral
                {
                    Id = op.Id,
                    ConsultId = RequireGuid(d, "consult_id"),
                    ReferredFor = RequireString(d, "referred_for"),
                    ReferralNotes = GetString(d, "referral_notes"),
                    ReferralState = ParseEnum<ReferralState>(GetString(d, "referral_state")) ?? ReferralState.New,
                    ReferralOutcome = GetString(d, "referral_outcome"),
                    CreatedAt = GetDateTimeOffset(d, "created_at") ?? DateTimeOffset.UtcNow,
                    ReferredToId = GetGuid(d, "referred_to_id"),
                    MemberInChargeId = GetGuid(d, "member_in_charge_id"),
                    GuardiansContacted = ParseEnum<GuardiansContacted>(GetString(d, "guardians_contacted")),
                    ClassOfService = ParseEnum<ReferralServiceClass>(GetString(d, "class_of_service")),
                    DescriptionOfService = GetString(d, "description_of_service"),
                    PatientSummaryFromScreening = GetString(d, "patient_summary_from_screening"),
                    RemarksAtBooth = GetString(d, "remarks_at_booth"),
                    MemberOnRunId = GetGuid(d, "member_on_run_id"),
                    PresentAtReferralParty = GetBool(d, "present_at_referral_party") ?? false,
                    ReferralPartyResults = GetString(d, "referral_party_results"),
                    FollowUp = GetBool(d, "follow_up") ?? false,
                    DoctorsUpdated = GetBool(d, "doctors_updated") ?? false,
                    AmountPaidUsd = NonNegative(GetDecimal(d, "amount_paid_usd"), "amount_paid_usd"),
                    AmountPaidRiels = NonNegative(GetInt(d, "amount_paid_riels"), "amount_paid_riels"),
                    MemberWhoPaidId = GetGuid(d, "member_who_paid_id"),
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "referrals", row.Id, "referred_for", row.ReferredFor, GetString(d, "referred_for") ?? row.ReferredFor, Has(d, "referred_for"), v => row.ReferredFor = v, userId);
        SetField(logConflict, "referrals", row.Id, "referral_notes", row.ReferralNotes, GetString(d, "referral_notes"), Has(d, "referral_notes"), v => row.ReferralNotes = v, userId);
        SetField(logConflict, "referrals", row.Id, "referral_state", row.ReferralState, ParseEnum<ReferralState>(GetString(d, "referral_state")) ?? row.ReferralState, Has(d, "referral_state"), v => row.ReferralState = v, userId);
        SetField(logConflict, "referrals", row.Id, "referral_outcome", row.ReferralOutcome, GetString(d, "referral_outcome"), Has(d, "referral_outcome"), v => row.ReferralOutcome = v, userId);

        // Booth section
        SetField(logConflict, "referrals", row.Id, "referred_to_id", row.ReferredToId, GetGuid(d, "referred_to_id"), Has(d, "referred_to_id"), v => row.ReferredToId = v, userId);
        SetField(logConflict, "referrals", row.Id, "member_in_charge_id", row.MemberInChargeId, GetGuid(d, "member_in_charge_id"), Has(d, "member_in_charge_id"), v => row.MemberInChargeId = v, userId);
        SetField(logConflict, "referrals", row.Id, "guardians_contacted", row.GuardiansContacted, ParseEnum<GuardiansContacted>(GetString(d, "guardians_contacted")), Has(d, "guardians_contacted"), v => row.GuardiansContacted = v, userId);
        SetField(logConflict, "referrals", row.Id, "class_of_service", row.ClassOfService, ParseEnum<ReferralServiceClass>(GetString(d, "class_of_service")), Has(d, "class_of_service"), v => row.ClassOfService = v, userId);
        SetField(logConflict, "referrals", row.Id, "description_of_service", row.DescriptionOfService, GetString(d, "description_of_service"), Has(d, "description_of_service"), v => row.DescriptionOfService = v, userId);
        SetField(logConflict, "referrals", row.Id, "patient_summary_from_screening", row.PatientSummaryFromScreening, GetString(d, "patient_summary_from_screening"), Has(d, "patient_summary_from_screening"), v => row.PatientSummaryFromScreening = v, userId);
        SetField(logConflict, "referrals", row.Id, "remarks_at_booth", row.RemarksAtBooth, GetString(d, "remarks_at_booth"), Has(d, "remarks_at_booth"), v => row.RemarksAtBooth = v, userId);

        // Referral party section
        SetField(logConflict, "referrals", row.Id, "member_on_run_id", row.MemberOnRunId, GetGuid(d, "member_on_run_id"), Has(d, "member_on_run_id"), v => row.MemberOnRunId = v, userId);
        SetField(logConflict, "referrals", row.Id, "present_at_referral_party", row.PresentAtReferralParty, GetBool(d, "present_at_referral_party") ?? false, Has(d, "present_at_referral_party"), v => row.PresentAtReferralParty = v, userId);
        SetField(logConflict, "referrals", row.Id, "referral_party_results", row.ReferralPartyResults, GetString(d, "referral_party_results"), Has(d, "referral_party_results"), v => row.ReferralPartyResults = v, userId);
        SetField(logConflict, "referrals", row.Id, "follow_up", row.FollowUp, GetBool(d, "follow_up") ?? false, Has(d, "follow_up"), v => row.FollowUp = v, userId);
        SetField(logConflict, "referrals", row.Id, "doctors_updated", row.DoctorsUpdated, GetBool(d, "doctors_updated") ?? false, Has(d, "doctors_updated"), v => row.DoctorsUpdated = v, userId);

        // Money section
        SetField(logConflict, "referrals", row.Id, "amount_paid_usd", row.AmountPaidUsd, NonNegative(GetDecimal(d, "amount_paid_usd"), "amount_paid_usd"), Has(d, "amount_paid_usd"), v => row.AmountPaidUsd = v, userId);
        SetField(logConflict, "referrals", row.Id, "amount_paid_riels", row.AmountPaidRiels, NonNegative(GetInt(d, "amount_paid_riels"), "amount_paid_riels"), Has(d, "amount_paid_riels"), v => row.AmountPaidRiels = v, userId);
        SetField(logConflict, "referrals", row.Id, "member_who_paid_id", row.MemberWhoPaidId, GetGuid(d, "member_who_paid_id"), Has(d, "member_who_paid_id"), v => row.MemberWhoPaidId = v, userId);
    }

    private async Task ApplyReferralPartnerAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.ReferralPartners.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
            {
                // Keep referral history intact. The REST delete endpoint refuses (409) when a
                // referral still points at the partner; an offline delete cannot be refused
                // interactively, so deactivate it instead and let the row sync back as inactive.
                if (await db.Referrals.AnyAsync(x => x.ReferredToId == existing.Id, ct))
                    existing.IsActive = false;
                else
                    db.ReferralPartners.Remove(existing);
            }
            return;
        }

        var d = RequireData(op);
        var row = await db.ReferralPartners.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.ReferralPartners.Add(
                new ReferralPartner
                {
                    Id = op.Id,
                    Name = RequireString(d, "name").Trim(),
                    Remarks = GetString(d, "remarks"),
                    IsActive = GetBool(d, "is_active") ?? true,
                    CreatedAt = GetDateTimeOffset(d, "created_at") ?? DateTimeOffset.UtcNow,
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "referral_partners", row.Id, "name", row.Name, GetString(d, "name")?.Trim() ?? row.Name, Has(d, "name"), v => row.Name = v, userId);
        SetField(logConflict, "referral_partners", row.Id, "remarks", row.Remarks, GetString(d, "remarks"), Has(d, "remarks"), v => row.Remarks = v, userId);
        SetField(logConflict, "referral_partners", row.Id, "is_active", row.IsActive, GetBool(d, "is_active") ?? row.IsActive, Has(d, "is_active"), v => row.IsActive = v, userId);
    }

    /// <summary>
    /// Tags are append-only and unique by normalized name. Two offline devices may both create
    /// "Follow-up"; the second insert is a no-op here and its patient links are re-pointed at
    /// the surviving tag by <see cref="ApplyPatientTagAsync"/> (which also receives the name).
    /// </summary>
    private async Task ApplyTagAsync(string verb, SyncCrudOp op, CancellationToken ct)
    {
        if (verb == "DELETE")
            return; // Tags are shared vocabulary; unlinking happens via patient_tags.

        var d = RequireData(op);
        if (await db.Tags.AnyAsync(x => x.Id == op.Id, ct))
            return;

        var name = TagSync.NormalizeNames([RequireString(d, "name")]).FirstOrDefault()
            ?? throw new BadRequestException("Tag name is required.");
        var normalized = name.ToUpperInvariant();
        if (await db.Tags.AnyAsync(x => x.NormalizedName == normalized, ct))
            return;

        db.Tags.Add(
            new Tag
            {
                Id = op.Id,
                Name = name,
                NormalizedName = normalized,
                CreatedAt = GetDateTimeOffset(d, "created_at") ?? DateTimeOffset.UtcNow,
            }
        );
    }

    private async Task ApplyPatientTagAsync(string verb, SyncCrudOp op, CancellationToken ct)
    {
        if (verb == "DELETE")
        {
            var existing = await db.PatientTags.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.PatientTags.Remove(existing);
            return;
        }

        var d = RequireData(op);
        if (await db.PatientTags.AnyAsync(x => x.Id == op.Id, ct))
            return; // Links are immutable; re-tagging is delete + insert.

        var patientId = RequireGuid(d, "patient_id");
        var tagId = GetGuid(d, "tag_id");
        var tag = tagId is { } tid ? await db.Tags.FirstOrDefaultAsync(x => x.Id == tid, ct) : null;
        if (tag is null)
        {
            // The client's tag lost a same-name race (see ApplyTagAsync): resolve by name.
            var name = GetString(d, "tag_name")
                ?? throw new BadRequestException("tag_id was not found and no tag_name was sent.");
            tag = (await TagSync.ResolveTagsAsync(db, TagSync.NormalizeNames([name]), ct)).FirstOrDefault()
                ?? throw new BadRequestException("Tag name is required.");
        }

        if (await db.PatientTags.AnyAsync(x => x.PatientId == patientId && x.TagId == tag.Id, ct))
            return; // Another device already linked this tag: keep one row.

        db.PatientTags.Add(new PatientTag { Id = op.Id, PatientId = patientId, Tag = tag });
    }

    private static T? NonNegative<T>(T? value, string field)
        where T : struct, IComparable<T>
    {
        if (value is { } v && v.CompareTo(default) < 0)
            throw new BadRequestException($"{field} cannot be negative.");
        return value;
    }

    private async Task ApplyIngredientAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb == "DELETE")
        {
            var existing = await db.MedicationActiveIngredients.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.MedicationActiveIngredients.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.MedicationActiveIngredients.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.MedicationActiveIngredients.Add(
                new MedicationActiveIngredient
                {
                    Id = op.Id,
                    Name = RequireString(d, "name"),
                    UnitOfMeasurement = RequireString(d, "unit_of_measurement"),
                    FallBelow = GetInt(d, "fall_below") ?? 0,
                    Remarks = GetString(d, "remarks"),
                    RxNormCode = GetString(d, "rx_norm_code"),
                    AtcCode = GetString(d, "atc_code"),
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "medication_active_ingredients", row.Id, "name", row.Name, GetString(d, "name") ?? row.Name, Has(d, "name"), v => row.Name = v, userId);
        SetField(logConflict, "medication_active_ingredients", row.Id, "unit_of_measurement", row.UnitOfMeasurement, GetString(d, "unit_of_measurement") ?? row.UnitOfMeasurement, Has(d, "unit_of_measurement"), v => row.UnitOfMeasurement = v, userId);
        SetField(logConflict, "medication_active_ingredients", row.Id, "fall_below", row.FallBelow, GetInt(d, "fall_below") ?? row.FallBelow, Has(d, "fall_below"), v => row.FallBelow = v, userId);
        SetField(logConflict, "medication_active_ingredients", row.Id, "remarks", row.Remarks, GetString(d, "remarks"), Has(d, "remarks"), v => row.Remarks = v, userId);
        SetField(logConflict, "medication_active_ingredients", row.Id, "rx_norm_code", row.RxNormCode, GetString(d, "rx_norm_code"), Has(d, "rx_norm_code"), v => row.RxNormCode = v, userId);
        SetField(logConflict, "medication_active_ingredients", row.Id, "atc_code", row.AtcCode, GetString(d, "atc_code"), Has(d, "atc_code"), v => row.AtcCode = v, userId);
    }

    private async Task ApplyBrandAsync(string verb, SyncCrudOp op, Guid userId, CancellationToken ct)
    {
        if (verb == "DELETE")
        {
            var existing = await db.MedicationBrands.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.MedicationBrands.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.MedicationBrands.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.MedicationBrands.Add(
                new MedicationBrand
                {
                    Id = op.Id,
                    Name = RequireString(d, "name"),
                    ActiveIngredientId = RequireGuid(d, "active_ingredient_id"),
                    Remarks = GetString(d, "remarks"),
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "medication_brands", row.Id, "name", row.Name, GetString(d, "name") ?? row.Name, Has(d, "name"), v => row.Name = v, userId);
        SetField(logConflict, "medication_brands", row.Id, "active_ingredient_id", row.ActiveIngredientId, GetGuid(d, "active_ingredient_id") ?? row.ActiveIngredientId, Has(d, "active_ingredient_id"), v => row.ActiveIngredientId = v, userId);
        SetField(logConflict, "medication_brands", row.Id, "remarks", row.Remarks, GetString(d, "remarks"), Has(d, "remarks"), v => row.Remarks = v, userId);
    }

    private async Task ApplyStockAsync(string verb, SyncCrudOp op, Guid userId, CancellationToken ct)
    {
        if (verb == "DELETE")
        {
            var existing = await db.MedicationStocks.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.MedicationStocks.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.MedicationStocks.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            db.MedicationStocks.Add(
                new MedicationStock
                {
                    Id = op.Id,
                    MedicationBrandId = RequireGuid(d, "medication_brand_id"),
                    Quantity = GetInt(d, "quantity") ?? 0,
                    Expiry = GetDateTimeOffset(d, "expiry") ?? DateTimeOffset.UtcNow,
                    Location = GetString(d, "location") ?? string.Empty,
                    StockStatus = ParseEnum<StockStatus>(GetString(d, "stock_status")) ?? StockStatus.Active,
                    Remarks = GetString(d, "remarks"),
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "medication_stocks", row.Id, "location", row.Location, GetString(d, "location") ?? row.Location, Has(d, "location"), v => row.Location = v, userId);
        SetField(logConflict, "medication_stocks", row.Id, "expiry", row.Expiry, GetDateTimeOffset(d, "expiry") ?? row.Expiry, Has(d, "expiry"), v => row.Expiry = v, userId);
        SetField(logConflict, "medication_stocks", row.Id, "stock_status", row.StockStatus, ParseEnum<StockStatus>(GetString(d, "stock_status")) ?? row.StockStatus, Has(d, "stock_status"), v => row.StockStatus = v, userId);
        SetField(logConflict, "medication_stocks", row.Id, "remarks", row.Remarks, GetString(d, "remarks"), Has(d, "remarks"), v => row.Remarks = v, userId);
    }

    private async Task ApplyStockChangeAsync(
        string verb,
        SyncCrudOp op,
        Guid userId,
        CancellationToken ct
    )
    {
        if (verb != "PUT")
            return;

        var existing = await db.StockChanges.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (existing is not null)
            return; // Idempotent replay

        var d = RequireData(op);
        var stockId = RequireGuid(d, "stock_id");
        var stock =
            await db.MedicationStocks.FirstOrDefaultAsync(x => x.Id == stockId, ct)
            ?? throw new NotFoundException("Medication stock was not found.");

        var field = ParseEnum<StockChangeField>(GetString(d, "field")) ?? StockChangeField.Quantity;
        var prevStr = GetString(d, "previous_value") ?? "0";
        var newStr = GetString(d, "new_value") ?? "0";

        if (
            field == StockChangeField.Quantity
            && int.TryParse(prevStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prevQty)
            && int.TryParse(newStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var newQty)
        )
        {
            // Apply as a delta so concurrent offline dispenses don't clobber each other.
            var delta = newQty - prevQty;
            var updated = stock.Quantity + delta;
            if (updated < 0)
                throw new ConflictException($"Insufficient stock in {stock.Location}.");

            prevStr = stock.Quantity.ToString(CultureInfo.InvariantCulture);
            newStr = updated.ToString(CultureInfo.InvariantCulture);
            stock.Quantity = updated;
        }

        db.StockChanges.Add(
            new StockChange
            {
                Id = op.Id,
                StockId = stockId,
                Field = field,
                PreviousValue = prevStr,
                NewValue = newStr,
                UserId = GetGuid(d, "user_id") ?? userId,
                CreatedAt = GetDateTimeOffset(d, "created_at") ?? DateTimeOffset.UtcNow,
            }
        );
    }

    private async Task ApplyOrderAsync(string verb, SyncCrudOp op, Guid userId, CancellationToken ct)
    {
        if (verb == "DELETE")
        {
            var existing = await db.Orders.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
            if (existing is not null)
                db.Orders.Remove(existing);
            return;
        }

        var d = RequireData(op);
        var row = await db.Orders.FirstOrDefaultAsync(x => x.Id == op.Id, ct);
        if (row is null)
        {
            var qty = GetInt(d, "quantity") ?? 1;
            if (qty <= 0)
                throw new BadRequestException("Order quantity must be positive.");

            db.Orders.Add(
                new Order
                {
                    Id = op.Id,
                    ConsultId = RequireGuid(d, "consult_id"),
                    MedicationBrandId = RequireGuid(d, "medication_brand_id"),
                    DosageInstructions = GetString(d, "dosage_instructions") ?? string.Empty,
                    Remarks = GetString(d, "remarks"),
                    Status = ParseEnum<OrderStatus>(GetString(d, "status")) ?? OrderStatus.Pending,
                    Quantity = qty,
                    CreatedAt = GetDateTimeOffset(d, "created_at") ?? DateTimeOffset.UtcNow,
                }
            );
            return;
        }

        var logConflict = IsStale(op, d, row.Version);
        SetField(logConflict, "orders", row.Id, "dosage_instructions", row.DosageInstructions, GetString(d, "dosage_instructions") ?? row.DosageInstructions, Has(d, "dosage_instructions"), v => row.DosageInstructions = v, userId);
        SetField(logConflict, "orders", row.Id, "remarks", row.Remarks, GetString(d, "remarks"), Has(d, "remarks"), v => row.Remarks = v, userId);
        SetField(logConflict, "orders", row.Id, "status", row.Status, ParseEnum<OrderStatus>(GetString(d, "status")) ?? row.Status, Has(d, "status"), v => row.Status = v, userId);
        SetField(logConflict, "orders", row.Id, "approved_by_id", row.ApprovedById, GetGuid(d, "approved_by_id"), Has(d, "approved_by_id"), v => row.ApprovedById = v, userId);
        SetField(logConflict, "orders", row.Id, "approved_at", row.ApprovedAt, GetDateTimeOffset(d, "approved_at"), Has(d, "approved_at"), v => row.ApprovedAt = v, userId);
    }

    private static bool IsStale(SyncCrudOp op, JsonElement d, int currentVersion)
    {
        var baseVersion = op.BaseVersion ?? GetInt(d, "version");
        return baseVersion is { } bv && currentVersion > bv;
    }

    private void SetField<T>(
        bool logConflict,
        string table,
        Guid recordId,
        string field,
        T currentValue,
        T incomingValue,
        bool hasField,
        Action<T> apply,
        Guid userId
    )
    {
        if (!hasField)
            return;

        if (!EqualityComparer<T>.Default.Equals(currentValue, incomingValue))
        {
            if (logConflict)
            {
                db.SyncConflicts.Add(
                    new SyncConflict
                    {
                        Table = table,
                        RecordId = recordId,
                        Field = field,
                        PreviousValue = FormatValue(currentValue),
                        IncomingValue = FormatValue(incomingValue),
                        WinningValue = FormatValue(incomingValue),
                        IncomingUserId = userId == Guid.Empty ? null : userId,
                    }
                );
            }
            apply(incomingValue);
        }
    }

    private static string? FormatValue<T>(T value) =>
        value switch
        {
            null => null,
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    private static JsonElement RequireData(SyncCrudOp op) =>
        op.Data ?? throw new BadRequestException($"Operation {op.Op} on {op.Type} requires data.");

    private static bool Has(JsonElement d, string name) =>
        d.ValueKind == JsonValueKind.Object && d.TryGetProperty(name, out _);

    private static string RequireString(JsonElement d, string name) =>
        GetString(d, name) ?? throw new BadRequestException($"{name} is required.");

    private static Guid RequireGuid(JsonElement d, string name) =>
        GetGuid(d, name) ?? throw new BadRequestException($"{name} is required.");

    private static string? GetString(JsonElement d, string name)
    {
        if (!d.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.GetRawText();
    }

    private static Guid? GetGuid(JsonElement d, string name)
    {
        var s = GetString(d, name);
        return Guid.TryParse(s, out var g) ? g : null;
    }

    private static int? GetInt(JsonElement d, string name)
    {
        if (!d.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i))
            return i;
        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static decimal? GetDecimal(JsonElement d, string name)
    {
        if (!d.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var dec))
            return dec;
        if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static bool? GetBool(JsonElement d, string name)
    {
        if (!d.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;
        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => prop.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => bool.TryParse(prop.GetString(), out var b)
                ? b
                : prop.GetString() == "1",
            _ => null,
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement d, string name)
    {
        var s = GetString(d, name);
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto
            : null;
    }

    private static DateOnly? GetDateOnly(JsonElement d, string name)
    {
        var s = GetString(d, name);
        if (DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            return DateOnly.FromDateTime(dto.UtcDateTime);
        return null;
    }

    private static TEnum? ParseEnum<TEnum>(string? raw)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) ? parsed : null;

    private static List<string>? GetStringList(JsonElement d, string name)
    {
        if (!d.TryGetProperty(name, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;
        if (prop.ValueKind == JsonValueKind.Array)
            return prop.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        if (prop.ValueKind == JsonValueKind.String)
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(prop.GetString() ?? "[]");
            }
            catch
            {
                return null;
            }
        }
        return null;
    }
}
