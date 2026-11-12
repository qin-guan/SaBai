//! Station records attached to a visit.

use rust_decimal::Decimal;

use crate::entity;
use crate::time::Ts;

entity! {
    pub struct Vitals in "Vitals", on_insert = stamp_vitals {
        id: i32 = "Id" [PkAuto],
        visit_id: i32 = "VisitId",
        height: Option<Decimal> = "Height",
        weight: Option<Decimal> = "Weight",
        temperature: Option<Decimal> = "Temperature",
        systolic: Option<i32> = "Systolic",
        diastolic: Option<i32> = "Diastolic",
        heart_rate: Option<i32> = "HeartRate",
        hemocue_count: Option<Decimal> = "HemocueCount",
        diabetes_mellitus: Option<bool> = "DiabetesMellitus",
        urine_test: Option<String> = "UrineTest",
        blood_glucose_non_fasting: Option<Decimal> = "BloodGlucoseNonFasting",
        blood_glucose_fasting: Option<Decimal> = "BloodGlucoseFasting",
        hba1c: Option<Decimal> = "Hba1c",
        others: Option<String> = "Others",
        /// When the record was first saved; stamped on insert, whichever endpoint creates
        /// it, so station throughput can be measured. Null for records from before it was tracked.
        recorded_at: Option<Ts> = "RecordedAt",
    }
}

fn stamp_vitals(vitals: &mut Vitals, now: Ts) {
    vitals.recorded_at.get_or_insert(now);
}

entity! {
    pub struct Eyesight in "Eyesights", on_insert = stamp_eyesight {
        id: i32 = "Id" [PkAuto],
        visit_id: i32 = "VisitId",
        left_eye_degree: Option<String> = "LeftEyeDegree",
        right_eye_degree: Option<String> = "RightEyeDegree",
        left_eye_pinhole: Option<String> = "LeftEyePinhole",
        right_eye_pinhole: Option<String> = "RightEyePinhole",
        left_astigmatism: Option<String> = "LeftAstigmatism",
        right_astigmatism: Option<String> = "RightAstigmatism",
        comments: Option<String> = "Comments",
        left_prescribed_glasses_degree: Option<String> = "LeftPrescribedGlassesDegree",
        right_prescribed_glasses_degree: Option<String> = "RightPrescribedGlassesDegree",
        /// When the record was first saved; stamped on insert.
        recorded_at: Option<Ts> = "RecordedAt",
    }
}

fn stamp_eyesight(eyesight: &mut Eyesight, now: Ts) {
    eyesight.recorded_at.get_or_insert(now);
}

entity! {
    pub struct PubertyAssessment in "PubertyAssessments" {
        id: i32 = "Id" [PkAuto],
        vitals_id: i32 = "VitalsId",
        pubarche: Option<bool> = "Pubarche",
        pubarche_age: Option<i32> = "PubarcheAge",
        thelarche: Option<bool> = "Thelarche",
        thelarche_age: Option<i32> = "ThelarcheAge",
        menarche: Option<bool> = "Menarche",
        menarche_age: Option<i32> = "MenarcheAge",
        voice_change: Option<bool> = "VoiceChange",
        voice_change_age: Option<i32> = "VoiceChangeAge",
        testicular_growth: Option<bool> = "TesticularGrowth",
        testicular_growth_age: Option<i32> = "TesticularGrowthAge",
        additional_notes: Option<String> = "AdditionalNotes",
    }
}

entity! {
    pub struct ChildVitals in "ChildVitals" {
        id: i32 = "Id" [PkAuto],
        vitals_id: i32 = "VitalsId",
        gross_motor: Option<String> = "GrossMotor",
        red_reflex: Option<String> = "RedReflex",
        scoliosis: Option<String> = "Scoliosis",
        pallor: Option<bool> = "Pallor",
        oral_cavity: Option<String> = "OralCavity",
        heart: Option<String> = "Heart",
        abdomen: Option<String> = "Abdomen",
        lungs: Option<String> = "Lungs",
        hernial_orifices: Option<String> = "HernialOrifices",
    }
}
