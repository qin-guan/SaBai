//! The dental station's consultation for a visit: history, chief complaint, clinical
//! examination, diagnoses, treatment provided, referral and remarks. One per visit.

use chrono::NaiveDate;

use crate::time::Ts;
use crate::{entity, wire_enum};

wire_enum! {
    pub enum YesNoUnknown {
        Yes = "Yes",
        No = "No",
        Unknown = "Unknown",
    }
}

wire_enum! {
    pub enum BrushingFrequency {
        Once = "Once",
        Twice = "Twice",
        ThreeOrMore = "ThreeOrMore",
        Occasionally = "Occasionally",
        Never = "Never",
    }
}

wire_enum! {
    pub enum SugarConsumption {
        Low = "Low",
        Moderate = "Moderate",
        High = "High",
    }
}

wire_enum! {
    pub enum AbsentPresent {
        Absent = "Absent",
        Present = "Present",
    }
}

wire_enum! {
    pub enum FacialSymmetry {
        Normal = "Normal",
        Asymmetrical = "Asymmetrical",
    }
}

wire_enum! {
    pub enum Lymphadenopathy {
        Absent = "Absent",
        Present = "Present",
        NotAssessed = "NotAssessed",
    }
}

wire_enum! {
    pub enum MouthOpening {
        Normal = "Normal",
        Limited = "Limited",
    }
}

wire_enum! {
    pub enum OralHygiene {
        Good = "Good",
        Fair = "Fair",
        Poor = "Poor",
    }
}

wire_enum! {
    pub enum PlaqueLevel {
        None = "None",
        Mild = "Mild",
        Moderate = "Moderate",
        Heavy = "Heavy",
    }
}

wire_enum! {
    pub enum GingivalCondition {
        Healthy = "Healthy",
        Inflamed = "Inflamed",
        Bleeding = "Bleeding",
    }
}

wire_enum! {
    pub enum CariesStatus {
        Absent = "Absent",
        NonCavitated = "NonCavitated",
        Cavitated = "Cavitated",
    }
}

wire_enum! {
    pub enum Malocclusion {
        NotSuspected = "NotSuspected",
        Suspected = "Suspected",
    }
}

wire_enum! {
    pub enum DentalReferralType {
        Restorative = "Restorative",
        Extraction = "Extraction",
        Perio = "Perio",
        Other = "Other",
        NotReferred = "NotReferred",
    }
}

wire_enum! {
    pub enum DentalReferralPriority {
        Urgent = "Urgent",
        Early = "Early",
        Routine = "Routine",
    }
}

entity! {
    pub struct DentalConsult in "DentalConsults", on_insert = stamp_recorded_at {
        id: i32 = "Id" [PkAuto],
        visit_id: i32 = "VisitId",
        previous_dental_visit: Option<YesNoUnknown> = "PreviousDentalVisit",
        previous_dental_treatment: Option<YesNoUnknown> = "PreviousDentalTreatment",
        toothache: Option<bool> = "Toothache",
        extraction: Option<bool> = "Extraction",
        dental_trauma: Option<bool> = "DentalTrauma",
        current_toothache: Option<bool> = "CurrentToothache",
        tooth_sensitivity: Option<bool> = "ToothSensitivity",
        bleeding_gums: Option<bool> = "BleedingGums",
        brushing_frequency: Option<BrushingFrequency> = "BrushingFrequency",
        fluoride_toothpaste: Option<YesNoUnknown> = "FluorideToothpaste",
        sugar_consumption: Option<SugarConsumption> = "SugarConsumption",
        other_dental_history: Option<String> = "OtherDentalHistory",
        chief_complaint: Option<String> = "ChiefComplaint",
        facial_symmetry: Option<FacialSymmetry> = "FacialSymmetry",
        facial_swelling: Option<AbsentPresent> = "FacialSwelling",
        lymphadenopathy: Option<Lymphadenopathy> = "Lymphadenopathy",
        mouth_opening: Option<MouthOpening> = "MouthOpening",
        oral_hygiene: Option<OralHygiene> = "OralHygiene",
        plaque: Option<PlaqueLevel> = "Plaque",
        calculus: Option<AbsentPresent> = "Calculus",
        gingival_condition: Option<GingivalCondition> = "GingivalCondition",
        caries: Option<CariesStatus> = "Caries",
        missing_teeth: Option<AbsentPresent> = "MissingTeeth",
        fractured_teeth: Option<AbsentPresent> = "FracturedTeeth",
        mucosal_lesions: Option<AbsentPresent> = "MucosalLesions",
        malocclusion: Option<Malocclusion> = "Malocclusion",
        diagnoses: Vec<String> = "Diagnoses",
        fluoride_done: Option<bool> = "FluorideDone",
        fluoride_date: Option<NaiveDate> = "FluorideDate",
        fluoride_operator: Option<String> = "FluorideOperator",
        fluoride_tooth_numbers: Option<String> = "FluorideToothNumbers",
        sealant_done: Option<bool> = "SealantDone",
        sealant_date: Option<NaiveDate> = "SealantDate",
        sealant_operator: Option<String> = "SealantOperator",
        sealant_tooth_numbers: Option<String> = "SealantToothNumbers",
        sdf_done: Option<bool> = "SdfDone",
        sdf_date: Option<NaiveDate> = "SdfDate",
        sdf_operator: Option<String> = "SdfOperator",
        sdf_tooth_numbers: Option<String> = "SdfToothNumbers",
        scaling_done: Option<bool> = "ScalingDone",
        scaling_date: Option<NaiveDate> = "ScalingDate",
        scaling_operator: Option<String> = "ScalingOperator",
        scaling_tooth_numbers: Option<String> = "ScalingToothNumbers",
        referred_for: Option<DentalReferralType> = "ReferredFor",
        referral_priority: Option<DentalReferralPriority> = "ReferralPriority",
        referral_notes: Option<String> = "ReferralNotes",
        recommended_facility: Option<String> = "RecommendedFacility",
        remarks: Option<String> = "Remarks",
        /// An uploaded image for the consultation (stored like any other file).
        image_file_id: Option<i32> = "ImageFileId",
        updated_at: Ts = "UpdatedAt",
        /// When the record was first saved; stamped by `the insert hook`. Null for records from before it was tracked.
        recorded_at: Option<Ts> = "RecordedAt",
    }
}

fn stamp_recorded_at(dental: &mut DentalConsult, now: Ts) {
    dental.recorded_at.get_or_insert(now);
}
