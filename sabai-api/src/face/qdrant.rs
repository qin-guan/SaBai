//! The face index in Qdrant: one collection (cosine distance), one point per face with
//! the payload `person_id` (keyword-indexed) and `created_at`.

use std::collections::HashMap;

use anyhow::{Context, anyhow, bail};
use async_trait::async_trait;
use qdrant_client::qdrant::point_id::PointIdOptions;
use qdrant_client::qdrant::vectors_config::Config as VectorsConfigKind;
use qdrant_client::qdrant::with_payload_selector::SelectorOptions;
use qdrant_client::qdrant::{
    Condition, CountPointsBuilder, CreateCollectionBuilder, CreateFieldIndexCollectionBuilder, DeletePointsBuilder,
    Distance, FieldType, Filter, GetPointsBuilder, PayloadIncludeSelector, PointId, PointStruct, PointsIdsList,
    QueryPointsBuilder, ScrollPointsBuilder, UpsertPointsBuilder, Value, VectorParamsBuilder,
};
use qdrant_client::{Payload, Qdrant};
use uuid::Uuid;

use super::store::{FaceIndex, FaceMatch, StoredFace};
use crate::config::QdrantSettings;
use crate::time::Ts;

const PERSON_ID_FIELD: &str = "person_id";
const CREATED_AT_FIELD: &str = "created_at";
const PAGE_SIZE: u32 = 256;

pub struct QdrantFaceIndex {
    client: Qdrant,
    collection: String,
}

impl QdrantFaceIndex {
    /// A client for the configured gRPC endpoint, or `None` when no endpoint is configured.
    /// Nothing is sent until the index is first used.
    pub fn from_settings(settings: &QdrantSettings, collection: &str) -> anyhow::Result<Option<Self>> {
        let Some(endpoint) = settings.endpoint.as_deref().filter(|endpoint| !endpoint.trim().is_empty()) else {
            return Ok(None);
        };
        let client = Qdrant::from_url(endpoint)
            .api_key(settings.api_key.clone())
            .skip_compatibility_check()
            .build()
            .with_context(|| format!("The Qdrant endpoint '{endpoint}' is not usable."))?;
        Ok(Some(QdrantFaceIndex { client, collection: collection.to_owned() }))
    }

    fn person_filter(person_id: &str) -> Filter {
        Filter::must([Condition::matches(PERSON_ID_FIELD, person_id.to_owned())])
    }
}

/// `2026-11-12T11:06:17.5681260+00:00`: the round-trip form faces have always been stamped with.
fn format_created_at(created_at: Ts) -> String {
    created_at.0.format("%Y-%m-%dT%H:%M:%S%.6f0+00:00").to_string()
}

fn face_id(id: Option<&PointId>) -> anyhow::Result<Uuid> {
    match id.and_then(|id| id.point_id_options.as_ref()) {
        Some(PointIdOptions::Uuid(text)) => Uuid::parse_str(text).with_context(|| format!("Face id '{text}' is not a UUID.")),
        _ => bail!("A point in the face collection has no UUID id."),
    }
}

fn text<'a>(payload: &'a HashMap<String, Value>, field: &str) -> anyhow::Result<&'a str> {
    payload
        .get(field)
        .and_then(|value| value.as_str())
        .map(String::as_str)
        .ok_or_else(|| anyhow!("A point in the face collection has no '{field}'."))
}

fn stored_face(id: Option<&PointId>, payload: &HashMap<String, Value>) -> anyhow::Result<StoredFace> {
    let created_at = text(payload, CREATED_AT_FIELD)?;
    Ok(StoredFace {
        face_id: face_id(id)?,
        person_id: text(payload, PERSON_ID_FIELD)?.to_owned(),
        created_at: Ts::parse(created_at).ok_or_else(|| anyhow!("Face timestamp '{created_at}' is not a date."))?,
    })
}

fn payload(face: &StoredFace) -> Payload {
    let mut fields: HashMap<String, Value> = HashMap::new();
    fields.insert(PERSON_ID_FIELD.to_owned(), face.person_id.clone().into());
    fields.insert(CREATED_AT_FIELD.to_owned(), format_created_at(face.created_at).into());
    fields.into()
}

#[async_trait]
impl FaceIndex for QdrantFaceIndex {
    async fn ensure_collection(&self, dimensions: usize) -> anyhow::Result<()> {
        let collection = self.collection.as_str();
        if self.client.collection_exists(collection).await? {
            let info = self.client.collection_info(collection).await?;
            let size = info
                .result
                .and_then(|info| info.config)
                .and_then(|config| config.params)
                .and_then(|params| params.vectors_config)
                .and_then(|vectors| vectors.config)
                .and_then(|kind| match kind {
                    VectorsConfigKind::Params(params) => Some(params.size),
                    VectorsConfigKind::ParamsMap(_) => None,
                });
            if size != Some(dimensions as u64) {
                bail!(
                    "Qdrant collection '{collection}' has vector size {}, but the recognition model produces {dimensions}.",
                    size.map(|size| size.to_string()).unwrap_or_default()
                );
            }
            return Ok(());
        }

        tracing::info!("Creating Qdrant collection {collection} ({dimensions}-d, cosine)");
        self.client
            .create_collection(
                CreateCollectionBuilder::new(collection)
                    .vectors_config(VectorParamsBuilder::new(dimensions as u64, Distance::Cosine)),
            )
            .await?;
        self.client
            .create_field_index(
                CreateFieldIndexCollectionBuilder::new(collection, PERSON_ID_FIELD, FieldType::Keyword).wait(true),
            )
            .await?;
        Ok(())
    }

    async fn add(&self, face: &StoredFace, embedding: &[f32]) -> anyhow::Result<()> {
        let point = PointStruct::new(face.face_id.to_string(), embedding.to_vec(), payload(face));
        self.client.upsert_points(UpsertPointsBuilder::new(self.collection.as_str(), vec![point]).wait(true)).await?;
        Ok(())
    }

    async fn search(&self, embedding: &[f32], min_similarity: f32, limit: u64) -> anyhow::Result<Vec<FaceMatch>> {
        let response = self
            .client
            .query(
                QueryPointsBuilder::new(self.collection.as_str())
                    .query(embedding.to_vec())
                    .limit(limit)
                    .score_threshold(min_similarity)
                    .with_payload(SelectorOptions::Include(PayloadIncludeSelector {
                        fields: vec![PERSON_ID_FIELD.to_owned()],
                    })),
            )
            .await?;
        response
            .result
            .iter()
            .map(|point| {
                Ok(FaceMatch {
                    face_id: face_id(point.id.as_ref())?,
                    person_id: text(&point.payload, PERSON_ID_FIELD)?.to_owned(),
                    similarity: point.score,
                })
            })
            .collect()
    }

    async fn list(&self, person_id: &str) -> anyhow::Result<Vec<StoredFace>> {
        let mut faces = Vec::new();
        let mut offset: Option<PointId> = None;
        loop {
            let mut request = ScrollPointsBuilder::new(self.collection.as_str())
                .filter(Self::person_filter(person_id))
                .limit(PAGE_SIZE)
                .with_payload(true);
            if let Some(offset) = offset.take() {
                request = request.offset(offset);
            }
            let page = self.client.scroll(request).await?;
            for point in &page.result {
                faces.push(stored_face(point.id.as_ref(), &point.payload)?);
            }
            offset = page.next_page_offset;
            if offset.is_none() {
                return Ok(faces);
            }
        }
    }

    async fn delete_face(&self, face_id: Uuid) -> anyhow::Result<bool> {
        let id = PointId::from(face_id.to_string());
        let existing = self
            .client
            .get_points(GetPointsBuilder::new(self.collection.as_str(), vec![id.clone()]).with_payload(false))
            .await?;
        if existing.result.is_empty() {
            return Ok(false);
        }
        self.client
            .delete_points(
                DeletePointsBuilder::new(self.collection.as_str()).points(PointsIdsList { ids: vec![id] }).wait(true),
            )
            .await?;
        Ok(true)
    }

    async fn delete_person(&self, person_id: &str) -> anyhow::Result<u64> {
        let count = self
            .client
            .count(CountPointsBuilder::new(self.collection.as_str()).filter(Self::person_filter(person_id)).exact(true))
            .await?
            .result
            .map(|result| result.count)
            .unwrap_or(0);
        if count > 0 {
            self.client
                .delete_points(
                    DeletePointsBuilder::new(self.collection.as_str()).points(Self::person_filter(person_id)).wait(true),
                )
                .await?;
        }
        Ok(count)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stamps_faces_in_the_round_trip_date_format() {
        let created_at = Ts::parse("2026-11-12T11:06:17.568126Z").unwrap();
        assert_eq!(format_created_at(created_at), "2026-11-12T11:06:17.5681260+00:00");
        assert_eq!(Ts::parse(&format_created_at(created_at)), Some(created_at));
    }

    #[test]
    fn reads_faces_enrolled_by_the_previous_api() {
        let id = PointId::from("3fa85f64-5717-4562-b3fc-2c963f66afa6");
        let mut fields: HashMap<String, Value> = HashMap::new();
        fields.insert("person_id".to_owned(), "patient:42".into());
        fields.insert("created_at".to_owned(), "2026-03-04T05:06:07.1234567+00:00".into());

        let face = stored_face(Some(&id), &fields).unwrap();

        assert_eq!(face.face_id, Uuid::parse_str("3fa85f64-5717-4562-b3fc-2c963f66afa6").unwrap());
        assert_eq!(face.person_id, "patient:42");
        assert_eq!(face.created_at, Ts::parse("2026-03-04T05:06:07.123456Z").unwrap());

        fields.remove("created_at");
        assert!(stored_face(Some(&id), &fields).is_err());
        assert!(stored_face(Some(&PointId::from(7u64)), &fields).is_err());
    }

    #[test]
    fn a_new_face_carries_the_same_payload_fields() {
        let face = StoredFace {
            face_id: Uuid::new_v4(),
            person_id: "p1".to_owned(),
            created_at: Ts::parse("2026-11-12T11:06:17.5Z").unwrap(),
        };
        let fields: HashMap<String, Value> = payload(&face).into();
        assert_eq!(fields.len(), 2);
        assert_eq!(text(&fields, "person_id").unwrap(), "p1");
        assert_eq!(text(&fields, "created_at").unwrap(), "2026-11-12T11:06:17.5000000+00:00");
        assert_eq!(stored_face(Some(&PointId::from(face.face_id.to_string())), &fields).unwrap(), face);
    }

    #[test]
    fn no_endpoint_means_no_index() {
        assert!(QdrantFaceIndex::from_settings(&QdrantSettings::default(), "faces").unwrap().is_none());
    }
}
