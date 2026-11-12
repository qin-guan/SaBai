//! Persists face embeddings in a vector index and performs nearest-neighbour identity search.
//!
//! [`FaceStore`] holds the rules (one best match per person, bounded candidate expansion,
//! once-only preparation of the collection); a [`FaceIndex`] holds the vectors. In
//! production that is Qdrant ([`super::qdrant`]); tests use [`InMemoryFaceIndex`].

use std::sync::{Arc, Mutex};

use anyhow::bail;
use async_trait::async_trait;
use serde::Serialize;
use tokio::sync::OnceCell;
use uuid::Uuid;

use super::analyzer::FaceAnalyzer;
use crate::time::Ts;

/// An enrolled face.
#[derive(Debug, Clone, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct StoredFace {
    pub face_id: Uuid,
    pub person_id: String,
    pub created_at: Ts,
}

#[derive(Debug, Clone, PartialEq)]
pub struct FaceMatch {
    pub face_id: Uuid,
    pub person_id: String,
    pub similarity: f32,
}

/// Where embeddings are kept: one vector per enrolled face, compared by cosine similarity.
#[async_trait]
pub trait FaceIndex: Send + Sync {
    /// Creates the collection (sized to the recognition model) if it does not exist yet;
    /// an existing collection of another size is an error.
    async fn ensure_collection(&self, dimensions: usize) -> anyhow::Result<()>;

    async fn add(&self, face: &StoredFace, embedding: &[f32]) -> anyhow::Result<()>;

    /// Up to `limit` faces with similarity of at least `min_similarity`, most similar first.
    async fn search(&self, embedding: &[f32], min_similarity: f32, limit: u64) -> anyhow::Result<Vec<FaceMatch>>;

    /// Every face of the person, in no particular order.
    async fn list(&self, person_id: &str) -> anyhow::Result<Vec<StoredFace>>;

    /// Removes one face; `false` when there is no such face.
    async fn delete_face(&self, face_id: Uuid) -> anyhow::Result<bool>;

    /// Removes every face of the person and returns how many there were.
    async fn delete_person(&self, person_id: &str) -> anyhow::Result<u64>;
}

pub struct FaceStore {
    index: Option<Arc<dyn FaceIndex>>,
    analyzer: Arc<FaceAnalyzer>,
    initialized: OnceCell<()>,
}

impl FaceStore {
    /// Without an index every operation fails, saying that none is configured.
    pub fn new(index: Option<Arc<dyn FaceIndex>>, analyzer: Arc<FaceAnalyzer>) -> Self {
        FaceStore { index, analyzer, initialized: OnceCell::new() }
    }

    /// Loads the models and creates the collection (sized to the recognition model) if it
    /// does not exist yet. Runs once; a failure is retried by the next caller.
    pub async fn initialize(&self) -> anyhow::Result<()> {
        self.ready().await.map(|_| ())
    }

    async fn ready(&self) -> anyhow::Result<&dyn FaceIndex> {
        self.initialized
            .get_or_try_init(|| async {
                // The models come first, so they are loaded even while the index is unreachable.
                let dimensions = self.analyzer.embedding_size().await?;
                self.index()?.ensure_collection(dimensions).await
            })
            .await?;
        self.index()
    }

    fn index(&self) -> anyhow::Result<&dyn FaceIndex> {
        match self.index.as_deref() {
            Some(index) => Ok(index),
            None => bail!("The face store is unavailable: no Qdrant connection is configured (ConnectionStrings:qdrant)."),
        }
    }

    pub async fn add(&self, person_id: &str, embedding: &[f32]) -> anyhow::Result<StoredFace> {
        let index = self.ready().await?;
        let face = StoredFace { face_id: Uuid::new_v4(), person_id: person_id.to_owned(), created_at: Ts::now() };
        index.add(&face, embedding).await?;
        Ok(face)
    }

    /// Returns the best match per person, most similar first.
    pub async fn search(&self, embedding: &[f32], min_similarity: f32, limit: i32) -> anyhow::Result<Vec<FaceMatch>> {
        let index = self.ready().await?;
        // A longitudinal photo history can have far more than five references per person.
        // Expand retrieval only when duplicate identities crowd out requested candidates.
        const MAXIMUM_CANDIDATES: i64 = 4096;
        let mut candidate_limit = (limit as i64 * 5).clamp(5, MAXIMUM_CANDIDATES);
        loop {
            let points = index.search(embedding, min_similarity, candidate_limit as u64).await?;
            let found = points.len() as i64;
            let matches = best_matches(points, limit);
            if matches.len() as i64 >= limit as i64 || found < candidate_limit || candidate_limit >= MAXIMUM_CANDIDATES {
                return Ok(matches);
            }
            candidate_limit = (candidate_limit * 2).min(MAXIMUM_CANDIDATES);
        }
    }

    /// The person's faces, oldest first.
    pub async fn list(&self, person_id: &str) -> anyhow::Result<Vec<StoredFace>> {
        let mut faces = self.ready().await?.list(person_id).await?;
        faces.sort_by_key(|face| face.created_at);
        Ok(faces)
    }

    pub async fn delete_face(&self, face_id: Uuid) -> anyhow::Result<bool> {
        self.ready().await?.delete_face(face_id).await
    }

    pub async fn delete_person(&self, person_id: &str) -> anyhow::Result<u64> {
        self.ready().await?.delete_person(person_id).await
    }
}

/// The strongest match of each person, most similar first, at most `limit` people.
pub(crate) fn best_matches(matches: impl IntoIterator<Item = FaceMatch>, limit: i32) -> Vec<FaceMatch> {
    let mut best: Vec<FaceMatch> = Vec::new();
    for candidate in matches {
        match best.iter_mut().find(|kept| kept.person_id == candidate.person_id) {
            Some(kept) if candidate.similarity > kept.similarity => *kept = candidate,
            Some(_) => {}
            None => best.push(candidate),
        }
    }
    best.sort_by(|a, b| b.similarity.partial_cmp(&a.similarity).unwrap_or(std::cmp::Ordering::Equal));
    best.truncate(limit.max(0) as usize);
    best
}

/// Keeps embeddings in memory and compares against all of them. For tests and local
/// experiments: nothing survives a restart.
#[derive(Default)]
pub struct InMemoryFaceIndex {
    state: Mutex<InMemory>,
}

#[derive(Default)]
struct InMemory {
    dimensions: Option<usize>,
    faces: Vec<(StoredFace, Vec<f32>)>,
}

impl InMemoryFaceIndex {
    pub fn new() -> Self {
        Self::default()
    }

    fn state(&self) -> std::sync::MutexGuard<'_, InMemory> {
        self.state.lock().unwrap_or_else(|poisoned| poisoned.into_inner())
    }
}

fn unit(vector: &[f32]) -> Vec<f32> {
    let norm = vector.iter().map(|&v| v as f64 * v as f64).sum::<f64>().sqrt();
    if norm > 0.0 { vector.iter().map(|&v| (v as f64 / norm) as f32).collect() } else { vector.to_vec() }
}

#[async_trait]
impl FaceIndex for InMemoryFaceIndex {
    async fn ensure_collection(&self, dimensions: usize) -> anyhow::Result<()> {
        let mut state = self.state();
        match state.dimensions {
            Some(size) if size != dimensions => {
                bail!("The face index has vector size {size}, but the recognition model produces {dimensions}.")
            }
            _ => state.dimensions = Some(dimensions),
        }
        Ok(())
    }

    async fn add(&self, face: &StoredFace, embedding: &[f32]) -> anyhow::Result<()> {
        let mut state = self.state();
        if state.dimensions.is_some_and(|size| size != embedding.len()) {
            bail!("The embedding has {} values, but the face index holds {}.", embedding.len(), state.dimensions.unwrap_or(0));
        }
        state.faces.push((face.clone(), unit(embedding)));
        Ok(())
    }

    async fn search(&self, embedding: &[f32], min_similarity: f32, limit: u64) -> anyhow::Result<Vec<FaceMatch>> {
        let query = unit(embedding);
        let mut matches: Vec<FaceMatch> = self
            .state()
            .faces
            .iter()
            .map(|(face, vector)| FaceMatch {
                face_id: face.face_id,
                person_id: face.person_id.clone(),
                similarity: super::embedder::similarity(&query, vector),
            })
            .filter(|found| found.similarity >= min_similarity)
            .collect();
        matches.sort_by(|a, b| b.similarity.partial_cmp(&a.similarity).unwrap_or(std::cmp::Ordering::Equal));
        matches.truncate(limit as usize);
        Ok(matches)
    }

    async fn list(&self, person_id: &str) -> anyhow::Result<Vec<StoredFace>> {
        Ok(self.state().faces.iter().filter(|(face, _)| face.person_id == person_id).map(|(face, _)| face.clone()).collect())
    }

    async fn delete_face(&self, face_id: Uuid) -> anyhow::Result<bool> {
        let mut state = self.state();
        let before = state.faces.len();
        state.faces.retain(|(face, _)| face.face_id != face_id);
        Ok(state.faces.len() < before)
    }

    async fn delete_person(&self, person_id: &str) -> anyhow::Result<u64> {
        let mut state = self.state();
        let before = state.faces.len();
        state.faces.retain(|(face, _)| face.person_id != person_id);
        Ok((before - state.faces.len()) as u64)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn found(person_id: &str, similarity: f32) -> FaceMatch {
        FaceMatch { face_id: Uuid::new_v4(), person_id: person_id.to_owned(), similarity }
    }

    #[test]
    fn best_matches_uses_strongest_reference_without_averaging_different_ages() {
        let old_photo = found("child-a", 0.45);
        let recent_photo = found("child-a", 0.72);
        let other_child = found("child-b", 0.61);

        let result = best_matches([old_photo, recent_photo.clone(), other_child], 2);

        assert_eq!(result.len(), 2);
        assert_eq!(result[0].face_id, recent_photo.face_id);
        assert_eq!(result[0].similarity, 0.72);
        assert_eq!(result[1].person_id, "child-b");
    }

    fn stored(person_id: &str) -> StoredFace {
        StoredFace { face_id: Uuid::new_v4(), person_id: person_id.to_owned(), created_at: Ts::now() }
    }

    #[tokio::test]
    async fn in_memory_index_searches_by_cosine_similarity() {
        let index = InMemoryFaceIndex::new();
        index.ensure_collection(2).await.unwrap();
        assert!(index.ensure_collection(3).await.is_err());

        let (a, b, c) = (stored("a"), stored("b"), stored("a"));
        index.add(&a, &[1.0, 0.0]).await.unwrap();
        index.add(&b, &[0.0, 2.0]).await.unwrap();
        index.add(&c, &[3.0, 3.0]).await.unwrap();
        assert!(index.add(&stored("d"), &[1.0, 0.0, 0.0]).await.is_err());

        let matches = index.search(&[5.0, 0.0], 0.5, 10).await.unwrap();
        assert_eq!(matches.iter().map(|m| m.face_id).collect::<Vec<_>>(), [a.face_id, c.face_id]);
        assert_eq!(matches[0].similarity, 1.0);
        assert!((matches[1].similarity - 0.5f32.sqrt()).abs() < 1e-6);
        assert_eq!(index.search(&[5.0, 0.0], -1.0, 1).await.unwrap().len(), 1);

        assert_eq!(index.list("a").await.unwrap().len(), 2);
        assert!(index.delete_face(c.face_id).await.unwrap());
        assert!(!index.delete_face(c.face_id).await.unwrap());
        assert_eq!(index.delete_person("a").await.unwrap(), 1);
        assert_eq!(index.delete_person("a").await.unwrap(), 0);
        assert_eq!(index.list("b").await.unwrap(), [b]);
    }
}
