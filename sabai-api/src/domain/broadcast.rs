use super::BroadcastSeverity;
use crate::entity;
use crate::time::Ts;

entity! {
    pub struct Broadcast in "Broadcasts" {
        id: i32 = "Id" [PkAuto],
        title: String = "Title",
        body: String = "Body",
        severity: BroadcastSeverity = "Severity",
        created_by: Option<String> = "CreatedBy",
        created_at: Ts = "CreatedAt",
        published_at: Option<Ts> = "PublishedAt",
        attempts: i32 = "Attempts",
    }
}
