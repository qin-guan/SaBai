package sabai.api.domain

import jakarta.persistence.Entity
import jakarta.persistence.GeneratedValue
import jakarta.persistence.GenerationType
import jakarta.persistence.Id
import jakarta.persistence.Table
import java.time.OffsetDateTime

@Entity
@Table(name = "VillageCodes")
class VillageCode(
    var code: String,
    var name: String,
    var colorHex: String,
    var isVisible: Boolean = true,
    var createdAt: OffsetDateTime,
    var updatedAt: OffsetDateTime,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0
}
