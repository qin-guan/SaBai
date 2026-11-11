using SaBai.API.Domain;

namespace SaBai.API.Endpoints.Dental.UpsertByVisit.Put;

internal static class Apply
{
    public static void To(DentalConsult entity, Request req)
    {
        entity.PreviousDentalVisit = req.PreviousDentalVisit;
        entity.PreviousDentalTreatment = req.PreviousDentalTreatment;
        entity.Toothache = req.Toothache;
        entity.Extraction = req.Extraction;
        entity.DentalTrauma = req.DentalTrauma;
        entity.CurrentToothache = req.CurrentToothache;
        entity.ToothSensitivity = req.ToothSensitivity;
        entity.BleedingGums = req.BleedingGums;
        entity.BrushingFrequency = req.BrushingFrequency;
        entity.FluorideToothpaste = req.FluorideToothpaste;
        entity.SugarConsumption = req.SugarConsumption;
        entity.OtherDentalHistory = FreeText.Normalize(req.OtherDentalHistory);
        entity.ChiefComplaint = FreeText.Normalize(req.ChiefComplaint);
        entity.FacialSymmetry = req.FacialSymmetry;
        entity.FacialSwelling = req.FacialSwelling;
        entity.Lymphadenopathy = req.Lymphadenopathy;
        entity.MouthOpening = req.MouthOpening;
        entity.OralHygiene = req.OralHygiene;
        entity.Plaque = req.Plaque;
        entity.Calculus = req.Calculus;
        entity.GingivalCondition = req.GingivalCondition;
        entity.Caries = req.Caries;
        entity.MissingTeeth = req.MissingTeeth;
        entity.FracturedTeeth = req.FracturedTeeth;
        entity.MucosalLesions = req.MucosalLesions;
        entity.Malocclusion = req.Malocclusion;
        entity.Diagnoses = (req.Diagnoses ?? []).Select(FreeText.Normalize).OfType<string>().ToList();
        entity.FluorideDone = req.FluorideDone;
        entity.FluorideDate = req.FluorideDate;
        entity.FluorideOperator = FreeText.Normalize(req.FluorideOperator);
        entity.FluorideToothNumbers = FreeText.Normalize(req.FluorideToothNumbers);
        entity.SealantDone = req.SealantDone;
        entity.SealantDate = req.SealantDate;
        entity.SealantOperator = FreeText.Normalize(req.SealantOperator);
        entity.SealantToothNumbers = FreeText.Normalize(req.SealantToothNumbers);
        entity.SdfDone = req.SdfDone;
        entity.SdfDate = req.SdfDate;
        entity.SdfOperator = FreeText.Normalize(req.SdfOperator);
        entity.SdfToothNumbers = FreeText.Normalize(req.SdfToothNumbers);
        entity.ScalingDone = req.ScalingDone;
        entity.ScalingDate = req.ScalingDate;
        entity.ScalingOperator = FreeText.Normalize(req.ScalingOperator);
        entity.ScalingToothNumbers = FreeText.Normalize(req.ScalingToothNumbers);
        entity.ReferredFor = req.ReferredFor;
        entity.ReferralPriority = req.ReferralPriority;
        entity.ReferralNotes = FreeText.Normalize(req.ReferralNotes);
        entity.RecommendedFacility = FreeText.Normalize(req.RecommendedFacility);
        entity.Remarks = FreeText.Normalize(req.Remarks);
        entity.ImageFileId = req.ImageFileId;
    }
}
