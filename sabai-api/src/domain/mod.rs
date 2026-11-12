//! Clinic data: one struct per table, plus the rules that belong to the data itself.

pub mod broadcast;
pub mod consults;
pub mod dental;
pub mod enums;
pub mod eyesight_rules;
pub mod patient;
pub mod pharmacy;
pub mod records;
pub mod stock_change_log;
pub mod stored_file;
pub mod village_code;
pub mod visit;

pub use broadcast::Broadcast;
pub use consults::{Consult, Diagnosis, Referral};
pub use dental::*;
pub use enums::*;
pub use patient::Patient;
pub use pharmacy::{MedicationActiveIngredient, MedicationBrand, MedicationStock, Order, OrderItem, StockChange};
pub use records::{ChildVitals, Eyesight, PubertyAssessment, Vitals};
pub use stored_file::StoredFile;
pub use village_code::VillageCode;
pub use visit::Visit;

pub use crate::auth::users::StaffUser;
pub use rust_decimal::Decimal;
