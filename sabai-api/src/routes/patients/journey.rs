//! Where a visit is in the clinic flow.

use std::collections::HashMap;

use serde::Serialize;
use sqlx::PgConnection;

use crate::domain::OrderStatus;
use crate::time::Ts;

/// Where a visit is in the clinic flow: vitals → vision → consult → dental
/// (optional) → pharmacy (optional). Lets staff see a patient's journey at a
/// glance without loading each station's record.
///
/// The timestamps let the client measure each station's throughput and estimate waits.
#[derive(Debug, Clone, Default, PartialEq, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct VisitJourney {
    /// Vitals have been recorded.
    pub vitals: bool,
    /// An eyesight record exists.
    pub vision: bool,
    /// At least one consult has been recorded.
    pub consult: bool,
    /// A dental consult has been recorded.
    pub dental: bool,
    /// A consult referred the patient to dental.
    pub dental_referred: bool,
    /// Medication orders from this visit's consults, excluding cancelled ones.
    pub pharmacy_orders: i32,
    /// Orders still waiting to be dispensed.
    pub pharmacy_pending: i32,
    /// When vitals were first recorded (null if unknown).
    pub vitals_at: Option<Ts>,
    /// When the eyesight record was first saved (null if unknown).
    pub vision_at: Option<Ts>,
    /// When the first consult was recorded.
    pub consult_at: Option<Ts>,
    /// When the dental consult was first saved (null if unknown).
    pub dental_at: Option<Ts>,
    /// When the last order was dispensed.
    pub pharmacy_at: Option<Ts>,
}

#[derive(sqlx::FromRow)]
struct Row {
    id: i32,
    #[sqlx(flatten)]
    journey: VisitJourney,
}

impl VisitJourney {
    /// Loads the journey of each visit in `visit_ids`, in one query.
    pub async fn for_visits(conn: &mut PgConnection, visit_ids: &[i32]) -> Result<HashMap<i32, VisitJourney>, sqlx::Error> {
        if visit_ids.is_empty() {
            return Ok(HashMap::new());
        }
        let rows: Vec<Row> = sqlx::query_as(
            r#"
            SELECT v."Id" AS id,
                EXISTS (SELECT 1 FROM "Vitals" t WHERE t."VisitId" = v."Id") AS vitals,
                EXISTS (SELECT 1 FROM "Eyesights" e WHERE e."VisitId" = v."Id") AS vision,
                EXISTS (SELECT 1 FROM "Consults" c WHERE c."VisitId" = v."Id") AS consult,
                EXISTS (SELECT 1 FROM "DentalConsults" d WHERE d."VisitId" = v."Id") AS dental,
                EXISTS (
                    SELECT 1 FROM "Consults" c
                    WHERE c."VisitId" = v."Id"
                      AND ((c."ReferredFor" IS NOT NULL AND strpos(lower(c."ReferredFor"), 'dental') > 0)
                           OR EXISTS (SELECT 1 FROM "Referrals" r
                                      WHERE r."ConsultId" = c."Id" AND strpos(lower(r."ReferredFor"), 'dental') > 0))
                ) AS dental_referred,
                (SELECT count(*)::int FROM "Consults" c JOIN "Orders" o ON o."ConsultId" = c."Id"
                  WHERE c."VisitId" = v."Id" AND o."Status" <> $2) AS pharmacy_orders,
                (SELECT count(*)::int FROM "Consults" c JOIN "Orders" o ON o."ConsultId" = c."Id"
                  WHERE c."VisitId" = v."Id" AND o."Status" = $3) AS pharmacy_pending,
                (SELECT t."RecordedAt" FROM "Vitals" t WHERE t."VisitId" = v."Id" LIMIT 1) AS vitals_at,
                (SELECT e."RecordedAt" FROM "Eyesights" e WHERE e."VisitId" = v."Id" LIMIT 1) AS vision_at,
                (SELECT min(c."Date") FROM "Consults" c WHERE c."VisitId" = v."Id") AS consult_at,
                (SELECT d."RecordedAt" FROM "DentalConsults" d WHERE d."VisitId" = v."Id" LIMIT 1) AS dental_at,
                (SELECT max(o."ApprovedAt") FROM "Consults" c JOIN "Orders" o ON o."ConsultId" = c."Id"
                  WHERE c."VisitId" = v."Id") AS pharmacy_at
            FROM "Visits" v
            WHERE v."Id" = ANY($1)
            "#,
        )
        .bind(visit_ids)
        .bind(OrderStatus::Cancelled)
        .bind(OrderStatus::Pending)
        .fetch_all(conn)
        .await?;
        Ok(rows.into_iter().map(|row| (row.id, row.journey)).collect())
    }
}
