using FastEndpoints;
using FluentValidation;

namespace SaBai.API.Endpoints.Dental.UpsertByVisit.Put;

public sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.PreviousDentalVisit).IsInEnum();
        RuleFor(x => x.PreviousDentalTreatment).IsInEnum();
        RuleFor(x => x.BrushingFrequency).IsInEnum();
        RuleFor(x => x.FluorideToothpaste).IsInEnum();
        RuleFor(x => x.SugarConsumption).IsInEnum();
        RuleFor(x => x.FacialSymmetry).IsInEnum();
        RuleFor(x => x.FacialSwelling).IsInEnum();
        RuleFor(x => x.Lymphadenopathy).IsInEnum();
        RuleFor(x => x.MouthOpening).IsInEnum();
        RuleFor(x => x.OralHygiene).IsInEnum();
        RuleFor(x => x.Plaque).IsInEnum();
        RuleFor(x => x.Calculus).IsInEnum();
        RuleFor(x => x.GingivalCondition).IsInEnum();
        RuleFor(x => x.Caries).IsInEnum();
        RuleFor(x => x.MissingTeeth).IsInEnum();
        RuleFor(x => x.FracturedTeeth).IsInEnum();
        RuleFor(x => x.MucosalLesions).IsInEnum();
        RuleFor(x => x.Malocclusion).IsInEnum();
        RuleFor(x => x.ReferredFor).IsInEnum();
        RuleFor(x => x.ReferralPriority).IsInEnum();
        RuleFor(x => x.OtherDentalHistory).MaximumLength(2000);
        RuleFor(x => x.ChiefComplaint).MaximumLength(2000);
        RuleFor(x => x.FluorideOperator).MaximumLength(200);
        RuleFor(x => x.FluorideToothNumbers).MaximumLength(200);
        RuleFor(x => x.SealantOperator).MaximumLength(200);
        RuleFor(x => x.SealantToothNumbers).MaximumLength(200);
        RuleFor(x => x.SdfOperator).MaximumLength(200);
        RuleFor(x => x.SdfToothNumbers).MaximumLength(200);
        RuleFor(x => x.ScalingOperator).MaximumLength(200);
        RuleFor(x => x.ScalingToothNumbers).MaximumLength(200);
        RuleFor(x => x.ReferralNotes).MaximumLength(2000);
        RuleFor(x => x.RecommendedFacility).MaximumLength(200);
        RuleFor(x => x.Remarks).MaximumLength(2000);
        RuleFor(x => x.ImageFileId).NotEmpty().When(x => x.ImageFileId is not null);
        RuleFor(x => x.Diagnoses).Must(x => x is null || x.Count <= 50).WithMessage("At most 50 diagnoses can be recorded.");
        RuleForEach(x => x.Diagnoses).NotNull().MaximumLength(500);
    }
}
