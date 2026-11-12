use chrono::Duration;
use uuid::Uuid;

use super::{DiagnosisCategory, ReferralState};
use crate::entity;
use crate::time::Ts;

entity! {
    pub struct Consult in "Consults" {
        id: i32 = "Id" [PkAuto],
        date: Ts = "Date",
        past_medical_history: Option<String> = "PastMedicalHistory",
        consultation: Option<String> = "Consultation",
        treatment_plan: Option<String> = "TreatmentPlan",
        referred_for: Option<String> = "ReferredFor",
        referral_notes: Option<String> = "ReferralNotes",
        remarks: Option<String> = "Remarks",
        doctor_id: Uuid = "DoctorId",
        visit_id: i32 = "VisitId",
    }
}

impl Consult {
    /// A consult can be edited for this long after it was written.
    pub fn edit_window() -> Duration {
        Duration::hours(24)
    }

    pub fn is_edit_locked(&self, now: Ts) -> bool {
        now - self.date > Self::edit_window()
    }
}

entity! {
    pub struct Diagnosis in "Diagnoses" {
        id: i32 = "Id" [PkAuto],
        details: String = "Details",
        category: Option<DiagnosisCategory> = "Category",
        consult_id: i32 = "ConsultId",
    }
}

entity! {
    pub struct Referral in "Referrals" {
        id: i32 = "Id" [PkAuto],
        referred_for: String = "ReferredFor",
        referral_notes: Option<String> = "ReferralNotes",
        referral_state: ReferralState = "ReferralState",
        referral_outcome: Option<String> = "ReferralOutcome",
        consult_id: i32 = "ConsultId",
        created_at: Ts = "CreatedAt",
    }
}
