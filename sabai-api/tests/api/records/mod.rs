//! Station records: vitals and what hangs off them, eyesight, glasses, vision, dental.

mod child_vitals;
mod dental;
mod eyesight;
mod glasses;
mod puberty;
mod vision;
mod vitals;
mod vitals_endpoint;

use serde_json::json;

use crate::support::*;

/// Records vitals on the visit and returns their id.
async fn create_vitals(client: &mut Client, visit_id: i64) -> i64 {
    id(&client.post("/vitals", json!({ "visitId": visit_id, "temperature": 36.5 })).await.ok().json())
}
