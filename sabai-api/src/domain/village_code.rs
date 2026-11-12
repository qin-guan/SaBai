use crate::entity;
use crate::time::Ts;

entity! {
    pub struct VillageCode in "VillageCodes" {
        id: i32 = "Id" [PkAuto],
        code: String = "Code",
        name: String = "Name",
        color_hex: String = "ColorHex",
        is_visible: bool = "IsVisible",
        created_at: Ts = "CreatedAt",
        updated_at: Ts = "UpdatedAt",
    }
}
