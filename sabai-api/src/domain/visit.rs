use crate::entity;
use crate::time::Ts;

entity! {
    pub struct Visit in "Visits" {
        id: i32 = "Id" [PkAuto],
        patient_id: i32 = "PatientId",
        village_code_id: i32 = "VillageCodeId",
        date: Ts = "Date",
        status: String = "Status",
    }
}

impl Visit {
    pub const STARTED_STATUS: &'static str = "started";
}
