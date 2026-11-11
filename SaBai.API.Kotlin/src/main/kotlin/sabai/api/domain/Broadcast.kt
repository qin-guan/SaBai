package sabai.api.domain

import jakarta.persistence.Entity
import jakarta.persistence.EnumType
import jakarta.persistence.Enumerated
import jakarta.persistence.GeneratedValue
import jakarta.persistence.GenerationType
import jakarta.persistence.Id
import jakarta.persistence.Table
import java.time.OffsetDateTime

@Entity
@Table(name = "Broadcasts")
class Broadcast(
    var title: String,
    var body: String,
    @Enumerated(EnumType.STRING) var severity: BroadcastSeverity = BroadcastSeverity.Info,
    var createdBy: String?,
    var createdAt: OffsetDateTime,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var publishedAt: OffsetDateTime? = null
    var attempts: Int = 0
}
