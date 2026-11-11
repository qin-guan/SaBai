package sabai.api.facerecognition

import io.qdrant.client.ConditionFactory.matchKeyword
import io.qdrant.client.PointIdFactory.id
import io.qdrant.client.QdrantClient
import io.qdrant.client.QueryFactory.nearest
import io.qdrant.client.ValueFactory.value
import io.qdrant.client.VectorsFactory.vectors
import io.qdrant.client.WithPayloadSelectorFactory.enable
import io.qdrant.client.WithPayloadSelectorFactory.include
import io.qdrant.client.grpc.Collections.Distance
import io.qdrant.client.grpc.Collections.PayloadSchemaType
import io.qdrant.client.grpc.Collections.VectorParams
import io.qdrant.client.grpc.Common.Filter
import io.qdrant.client.grpc.Common.PointId
import io.qdrant.client.grpc.Points.PointStruct
import io.qdrant.client.grpc.Points.QueryPoints
import io.qdrant.client.grpc.Points.ScrollPoints
import org.slf4j.LoggerFactory
import org.springframework.stereotype.Component
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

data class StoredFace(val faceId: UUID, val personId: String, val createdAt: OffsetDateTime)

data class FaceMatch(val faceId: UUID, val personId: String, val similarity: Float)

/**
 * Face embeddings in Qdrant, with nearest-neighbour identity search. The client returns
 * futures; `.get()` simply blocks the (virtual) request thread.
 */
@Component
class FaceStore(private val qdrant: QdrantClient, private val analyzer: FaceAnalyzer, options: FaceRecognitionOptions) {
    private val log = LoggerFactory.getLogger(javaClass)
    private val collection = options.collectionName

    /** Creates the collection (sized to the recognition model) once, on first use. */
    private val ready by lazy { ensureCollection(analyzer.embeddingSize) }

    fun initialize() = ready

    private fun ensureCollection(dimensions: Int) {
        if (qdrant.collectionExistsAsync(collection).get()) {
            val size = qdrant.getCollectionInfoAsync(collection).get().config.params.vectorsConfig.params.size
            check(size == dimensions.toLong()) {
                "Qdrant collection '$collection' has vector size $size, but the recognition model produces $dimensions."
            }
            return
        }
        log.info("Creating Qdrant collection {} ({}-d, cosine)", collection, dimensions)
        qdrant.createCollectionAsync(
            collection,
            VectorParams.newBuilder().setSize(dimensions.toLong()).setDistance(Distance.Cosine).build(),
        ).get()
        qdrant.createPayloadIndexAsync(collection, PERSON_ID, PayloadSchemaType.Keyword, null, null, null, null).get()
    }

    fun add(personId: String, embedding: FloatArray): StoredFace {
        ready
        val face = StoredFace(UUID.randomUUID(), personId, OffsetDateTime.now(ZoneOffset.UTC))
        val point =
            PointStruct.newBuilder()
                .setId(id(face.faceId))
                .setVectors(vectors(*embedding))
                .putAllPayload(mapOf(PERSON_ID to value(personId), CREATED_AT to value(face.createdAt.toString())))
                .build()
        qdrant.upsertAsync(collection, listOf(point)).get()
        return face
    }

    /** The best match per person, most similar first. */
    fun search(embedding: FloatArray, minSimilarity: Float, limit: Int): List<FaceMatch> {
        ready
        // A long photo history can have many references per person; widen retrieval only
        // when duplicate identities crowd out the requested number of candidates.
        var candidateLimit = (limit.toLong() * 5).coerceIn(5, MAX_CANDIDATES)
        while (true) {
            val query =
                QueryPoints.newBuilder()
                    .setCollectionName(collection)
                    .setQuery(nearest(*embedding))
                    .setLimit(candidateLimit)
                    .setScoreThreshold(minSimilarity)
                    .setWithPayload(include(listOf(PERSON_ID)))
                    .build()
            val points = qdrant.queryAsync(query).get()
            val matches =
                bestMatches(
                    points.map { FaceMatch(UUID.fromString(it.id.uuid), it.payloadMap.getValue(PERSON_ID).stringValue, it.score) },
                    limit,
                )
            if (matches.size >= limit || points.size < candidateLimit || candidateLimit >= MAX_CANDIDATES) return matches
            candidateLimit = minOf(candidateLimit * 2, MAX_CANDIDATES)
        }
    }

    fun list(personId: String): List<StoredFace> {
        ready
        val faces = mutableListOf<StoredFace>()
        var offset: PointId? = null
        do {
            val request =
                ScrollPoints.newBuilder()
                    .setCollectionName(collection)
                    .setFilter(byPerson(personId))
                    .setLimit(256)
                    .setWithPayload(enable(true))
                    .apply { offset?.let(::setOffset) }
                    .build()
            val page = qdrant.scrollAsync(request).get()
            faces +=
                page.resultList.map {
                    StoredFace(
                        UUID.fromString(it.id.uuid),
                        it.payloadMap.getValue(PERSON_ID).stringValue,
                        OffsetDateTime.parse(it.payloadMap.getValue(CREATED_AT).stringValue),
                    )
                }
            offset = if (page.hasNextPageOffset()) page.nextPageOffset else null
        } while (offset != null)
        return faces.sortedBy { it.createdAt }
    }

    fun deleteFace(faceId: UUID): Boolean {
        ready
        if (qdrant.retrieveAsync(collection, id(faceId), false, false, null).get().isEmpty()) return false
        qdrant.deleteAsync(collection, listOf(id(faceId))).get()
        return true
    }

    fun deletePerson(personId: String): Long {
        ready
        val count = qdrant.countAsync(collection, byPerson(personId), true).get()
        if (count > 0) qdrant.deleteAsync(collection, byPerson(personId)).get()
        return count
    }

    private fun byPerson(personId: String) = Filter.newBuilder().addMust(matchKeyword(PERSON_ID, personId)).build()

    companion object {
        private const val PERSON_ID = "person_id"
        private const val CREATED_AT = "created_at"
        private const val MAX_CANDIDATES = 4096L

        fun bestMatches(matches: List<FaceMatch>, limit: Int): List<FaceMatch> =
            matches
                .groupBy { it.personId }
                .map { (_, faces) -> faces.maxBy { it.similarity } }
                .sortedByDescending { it.similarity }
                .take(limit)
    }
}
