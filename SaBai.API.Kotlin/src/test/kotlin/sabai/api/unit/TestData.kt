package sabai.api.unit

import sabai.api.domain.Gender
import sabai.api.domain.Patient
import sabai.api.domain.StaffUser
import sabai.api.domain.VillageCode
import sabai.api.domain.Visit
import java.time.OffsetDateTime
import java.util.UUID

object TestData {
    private val now: OffsetDateTime = OffsetDateTime.parse("2026-11-01T08:00:00Z")

    fun doctor() = StaffUser(UUID.randomUUID(), "doctor", "Doctor", now)

    fun visit(): Visit {
        val village = VillageCode("SB", "Sabai", "#ABCDEF", createdAt = now, updatedAt = now)
        val patient = Patient("Ada", Gender.Female, now.minusYears(30), "none", village, now)
        return Visit(patient, village, now)
    }
}
