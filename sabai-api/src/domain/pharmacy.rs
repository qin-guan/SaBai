use uuid::Uuid;

use super::{OrderStatus, StockChangeField, StockStatus};
use crate::entity;
use crate::time::Ts;

entity! {
    pub struct MedicationActiveIngredient in "MedicationActiveIngredients" {
        id: i32 = "Id" [PkAuto],
        name: String = "Name",
        unit_of_measurement: String = "UnitOfMeasurement",
        fall_below: i32 = "FallBelow",
        remarks: Option<String> = "Remarks",
    }
}

entity! {
    pub struct MedicationBrand in "MedicationBrands" {
        id: i32 = "Id" [PkAuto],
        name: String = "Name",
        active_ingredient_id: i32 = "ActiveIngredientId",
        remarks: Option<String> = "Remarks",
    }
}

entity! {
    pub struct MedicationStock in "MedicationStocks" {
        id: i32 = "Id" [PkAuto],
        medication_brand_id: i32 = "MedicationBrandId",
        quantity: i32 = "Quantity",
        expiry: Ts = "Expiry",
        location: String = "Location",
        stock_status: StockStatus = "StockStatus",
        remarks: Option<String> = "Remarks",
    }
}

entity! {
    pub struct StockChange in "StockChanges" {
        id: i32 = "Id" [PkAuto],
        stock_id: i32 = "StockId",
        field: StockChangeField = "Field",
        previous_value: String = "PreviousValue",
        new_value: String = "NewValue",
        user_id: Uuid = "UserId",
        created_at: Ts = "CreatedAt",
    }
}

entity! {
    pub struct Order in "Orders" {
        id: i32 = "Id" [PkAuto],
        consult_id: i32 = "ConsultId",
        medication_brand_id: i32 = "MedicationBrandId",
        dosage_instructions: String = "DosageInstructions",
        remarks: Option<String> = "Remarks",
        status: OrderStatus = "Status",
        quantity: i32 = "Quantity",
        approved_by_id: Option<Uuid> = "ApprovedById",
        approved_at: Option<Ts> = "ApprovedAt",
        created_at: Ts = "CreatedAt",
    }
}

entity! {
    pub struct OrderItem in "OrderItems" {
        order_id: i32 = "OrderId" [PkFk],
        stock_change_id: i32 = "StockChangeId" [PkFk],
    }
}
