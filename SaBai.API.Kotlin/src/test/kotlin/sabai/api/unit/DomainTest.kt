package sabai.api.unit

import org.assertj.core.api.Assertions.assertThat
import org.junit.jupiter.api.Test
import sabai.api.data.AuditListener
import sabai.api.domain.Consult
import sabai.api.domain.Patient
import sabai.api.endpoints.patients.PatientFaceIdentity
import java.math.BigDecimal
import java.time.OffsetDateTime
import java.time.ZoneOffset

class DomainTest {
    @Test
    fun `display ids pad to four digits and prefix the village code`() {
        assertThat(Patient.formatDisplayId("SB", 42)).isEqualTo("SB0042")
        assertThat(Patient.formatDisplayId(null, 7)).isEqualTo("0007")
        assertThat(Patient.formatDisplayId(" ", 12345)).isEqualTo("12345")
        assertThat(Patient.formatFilterString("SB", 42, "555", "Ada")).isEqualTo("SB0042 SB42 555 Ada")
    }

    @Test
    fun `consults lock 24 hours after they were recorded`() {
        val recorded = OffsetDateTime.of(2026, 11, 1, 8, 0, 0, 0, ZoneOffset.UTC)
        val consult = Consult(recorded, doctor = TestData.doctor(), visit = TestData.visit())
        assertThat(consult.isEditLocked(recorded.plusHours(23))).isFalse()
        assertThat(consult.isEditLocked(recorded.plusHours(24).plusSeconds(1))).isTrue()
    }

    @Test
    fun `patient face identities are namespaced`() {
        assertThat(PatientFaceIdentity.forPatient(42)).isEqualTo("patient:42")
        assertThat(PatientFaceIdentity.parse("patient:42")).isEqualTo(42)
        assertThat(PatientFaceIdentity.parse("person:42")).isNull()
        assertThat(PatientFaceIdentity.parse("patient:x")).isNull()
        assertThat(PatientFaceIdentity.parse(null)).isNull()
    }

    @Test
    fun `audit values are formatted invariantly and truncated`() {
        assertThat(AuditListener.format(BigDecimal("36.60"))).isEqualTo("36.60")
        assertThat(AuditListener.format(true)).isEqualTo("true")
        assertThat(AuditListener.format(ByteArray(3))).isEqualTo("[3 bytes]")
        assertThat(AuditListener.format("x".repeat(3000))).hasSize(AuditListener.MAX_VALUE_LENGTH).endsWith("…")
    }
}
