use super::Gender;
use crate::entity;
use crate::time::Ts;

entity! {
    pub struct Patient in "Patients" {
        id: i32 = "Id" [PkAuto],
        village_code_id: Option<i32> = "VillageCodeId",
        name: String = "Name",
        identification_number: Option<String> = "IdentificationNumber",
        contact_no: Option<String> = "ContactNo",
        gender: Gender = "Gender",
        date_of_birth: Ts = "DateOfBirth",
        drug_allergy: String = "DrugAllergy",
        has_poor_card: bool = "HasPoorCard",
        has_bs2_card: bool = "HasBS2Card",
        has_sabai_card: bool = "HasSabaiCard",
        to_get_report: bool = "ToGetReport",
        /// RustFS object key of the patient's profile photo, or null when none is set.
        /// Kept in the legacy `PatientImagePublicId` column.
        photo_stored_file_name: Option<String> = "PatientImagePublicId" as "PhotoStoredFileName",
        /// Qdrant face id most recently enrolled for this patient, or null. Used only as
        /// a "has a face" flag; all faces live in the face store. Kept in the legacy
        /// `RekognitionFaceId` column.
        latest_face_id: Option<String> = "RekognitionFaceId" as "LatestFaceId",
        created_at: Ts = "CreatedAt",
    }
}

impl Patient {
    /// `VC0012`, or `0012` without a village code.
    pub fn format_display_id(village_code: Option<&str>, id: i32) -> String {
        match village_code.filter(|code| !code.trim().is_empty()) {
            Some(code) => format!("{code}{id:04}"),
            None => format!("{id:04}"),
        }
    }

    /// The text patient search matches against: both id forms, contact number and name.
    pub fn format_filter_string(village_code: Option<&str>, id: i32, contact_no: Option<&str>, name: &str) -> String {
        let display = Self::format_display_id(village_code, id);
        let compact = match village_code.filter(|code| !code.trim().is_empty()) {
            Some(code) => format!("{code}{id}"),
            None => id.to_string(),
        };
        format!("{display} {compact} {} {name}", contact_no.unwrap_or(""))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn formats_ids() {
        assert_eq!(Patient::format_display_id(Some("A1"), 1), "A10001");
        assert_eq!(Patient::format_display_id(None, 12345), "12345");
        assert_eq!(Patient::format_display_id(Some(" "), 7), "0007");
        assert_eq!(Patient::format_filter_string(Some("A1"), 1, None, "P"), "A10001 A11  P");
        assert_eq!(Patient::format_filter_string(None, 7, Some("555"), "P"), "0007 7 555 P");
    }
}
