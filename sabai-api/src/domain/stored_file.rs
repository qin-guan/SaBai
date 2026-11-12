use crate::entity;
use crate::time::Ts;

entity! {
    /// A file kept in the RustFS bucket. Patient documents, dental images and any other
    /// upload share this table; the bytes live in the bucket under `stored_file_name`.
    pub struct StoredFile in "Files" {
        id: i32 = "Id" [PkAuto],
        /// The patient the file belongs to. Null for files that are not about a patient.
        patient_id: Option<i32> = "PatientId",
        file_name: String = "FileName",
        stored_file_name: String = "StoredFileName",
        description: String = "Description",
        size_bytes: i64 = "SizeBytes",
        is_deleted: bool = "IsDeleted",
        created_at: Ts = "CreatedAt",
    }
}

impl StoredFile {
    pub const MAX_BYTES: i64 = 26_214_400;

    /// Content types a clinical file may be uploaded as: PDF, JPEG, PNG and DOCX.
    pub const ALLOWED_CONTENT_TYPES: &'static [&'static str] = &[
        "application/pdf",
        "image/jpeg",
        "image/png",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    ];

    pub fn is_allowed_content_type(content_type: &str) -> bool {
        Self::ALLOWED_CONTENT_TYPES.iter().any(|allowed| allowed.eq_ignore_ascii_case(content_type))
    }
}
