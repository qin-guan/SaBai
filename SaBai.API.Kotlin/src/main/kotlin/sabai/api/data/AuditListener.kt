package sabai.api.data

import jakarta.annotation.PostConstruct
import jakarta.persistence.EntityManagerFactory
import org.hibernate.engine.spi.SessionFactoryImplementor
import org.hibernate.engine.spi.SharedSessionContractImplementor
import org.hibernate.engine.spi.TransactionCompletionCallbacks.BeforeCompletionCallback
import org.hibernate.event.service.spi.EventListenerRegistry
import org.hibernate.event.spi.EventType
import org.hibernate.event.spi.PostDeleteEvent
import org.hibernate.event.spi.PostDeleteEventListener
import org.hibernate.event.spi.PostInsertEvent
import org.hibernate.event.spi.PostInsertEventListener
import org.hibernate.event.spi.PostUpdateEvent
import org.hibernate.event.spi.PostUpdateEventListener
import org.hibernate.persister.entity.EntityPersister
import org.hibernate.type.OneToOneType
import org.springframework.stereotype.Component
import sabai.api.domain.AuditAction
import sabai.api.domain.AuditFieldChange
import sabai.api.domain.AuditLogEntry
import sabai.api.domain.UserToken
import java.math.BigDecimal
import java.time.Clock
import java.time.OffsetDateTime
import java.time.temporal.TemporalAccessor
import java.util.Collections
import java.util.WeakHashMap

/**
 * Writes an audit entry for every row inserted, updated or deleted through Hibernate,
 * attributed to the signed-in staff user (the C# AuditInterceptor).
 *
 * Hibernate fires these events after each SQL statement, so generated ids are already
 * known: no second SaveChanges pass is needed. Entries are buffered per session and
 * inserted just before the transaction commits, so they commit or roll back with the
 * change they describe. Bulk JPQL updates/deletes bypass these events.
 */
@Component
class AuditListener(
    private val writer: AuditLogWriter,
    private val clock: Clock,
    private val entityManagerFactory: EntityManagerFactory,
) : PostInsertEventListener, PostUpdateEventListener, PostDeleteEventListener {
    private val pending =
        Collections.synchronizedMap(WeakHashMap<SharedSessionContractImplementor, MutableList<AuditLogEntry>>())

    @PostConstruct
    fun register() {
        val registry =
            entityManagerFactory
                .unwrap(SessionFactoryImplementor::class.java)
                .serviceRegistry
                .requireService(EventListenerRegistry::class.java)
        registry.appendListeners(EventType.POST_INSERT, this)
        registry.appendListeners(EventType.POST_UPDATE, this)
        registry.appendListeners(EventType.POST_DELETE, this)
    }

    override fun onPostInsert(event: PostInsertEvent) =
        record(event.session, event.persister, event.id, AuditAction.Created, null, event.state, null)

    override fun onPostUpdate(event: PostUpdateEvent) =
        record(event.session, event.persister, event.id, AuditAction.Updated, event.oldState, event.state, event.dirtyProperties)

    override fun onPostDelete(event: PostDeleteEvent) =
        record(event.session, event.persister, event.id, AuditAction.Deleted, event.deletedState, null, null)

    override fun requiresPostCommitHandling(persister: EntityPersister) = false

    private fun record(
        session: SharedSessionContractImplementor,
        persister: EntityPersister,
        id: Any?,
        action: AuditAction,
        old: Array<Any?>?,
        new: Array<Any?>?,
        dirty: IntArray?,
    ) {
        val redactAll = persister.mappedClass == UserToken::class.java
        val changes =
            persister.propertyNames.indices.mapNotNull { i ->
                val name = persister.propertyNames[i]
                val type = persister.propertyTypes[i]
                // Collections and the inverse side of one-to-ones are not columns of this row.
                if (type.isCollectionType || type is OneToOneType || name in IGNORED) return@mapNotNull null
                if (action == AuditAction.Updated && dirty?.contains(i) != true) return@mapNotNull null

                val field = if (type.isEntityType) "${name.pascal()}Id" else name.pascal()
                val before = old?.get(i)?.let { if (type.isEntityType) idOf(it) else it }
                val after = new?.get(i)?.let { if (type.isEntityType) idOf(it) else it }
                if (before == null && after == null || action == AuditAction.Updated && before == after) {
                    return@mapNotNull null
                }
                val secret = redactAll || name in SECRETS
                AuditFieldChange(field, render(before, secret), render(after, secret))
            }

        // Nothing a reader would see changed.
        if (action == AuditAction.Updated && changes.isEmpty()) return

        val actor = AuditActor.current()
        enqueue(
            session,
            AuditLogEntry(
                occurredAt = OffsetDateTime.now(clock),
                action = action,
                userId = actor.userId,
                userName = actor.userName,
                entityType = persister.mappedClass.simpleName,
                entityId = keyOf(id),
                changes = changes,
                ipAddress = actor.ipAddress,
                request = actor.request,
            ),
        )
    }

    private fun enqueue(session: SharedSessionContractImplementor, entry: AuditLogEntry) {
        val buffer =
            pending.getOrPut(session) {
                session.transactionCompletionCallbacks.registerCallback(BeforeCompletionCallback(::flush))
                mutableListOf()
            }
        buffer += entry
    }

    private fun flush(session: SharedSessionContractImplementor) {
        val entries = pending.remove(session) ?: return
        session.doWork { connection -> writer.insert(connection, entries) }
    }

    private fun idOf(entity: Any): Any? = entityManagerFactory.persistenceUnitUtil.getIdentifier(entity)

    /** Composite keys (`UserToken.Key`) are comma-separated, like the C# API. */
    private fun keyOf(id: Any?): String? =
        when (id) {
            is UserToken.Key -> listOf(id.userId, id.loginProvider, id.name).joinToString(",")
            else -> format(id)
        }

    private fun render(value: Any?, secret: Boolean): String? =
        if (secret) value?.let { REDACTED } else format(value)

    companion object {
        const val MAX_VALUE_LENGTH = 2000
        const val REDACTED = "[redacted]"
        private val IGNORED = setOf("concurrencyStamp")
        private val SECRETS = setOf("passwordHash", "securityStamp")

        private fun String.pascal() = replaceFirstChar { it.uppercaseChar() }

        fun format(value: Any?): String? {
            val text =
                when (value) {
                    null -> return null
                    is String -> value
                    is TemporalAccessor -> value.toString()
                    is BigDecimal -> value.toPlainString()
                    is Enum<*> -> value.name
                    is ByteArray -> "[${value.size} bytes]"
                    else -> value.toString()
                }
            return if (text.length > MAX_VALUE_LENGTH) text.take(MAX_VALUE_LENGTH - 1) + "…" else text
        }
    }
}
